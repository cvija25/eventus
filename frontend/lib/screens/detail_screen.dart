import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:fl_chart/fl_chart.dart';
import '../models/item.dart';
import '../models/price_history_point.dart';
import '../services/api_service.dart';
import 'balance_screen.dart';
import '../utils/color_utils.dart';
import '../services/auth_service.dart';
import '../services/sse_service.dart';

import 'dart:async';

class DetailScreen extends StatefulWidget {
  final Item item;

  const DetailScreen({super.key, required this.item});

  @override
  State<DetailScreen> createState() => _DetailScreenState();
}

class _DetailScreenState extends State<DetailScreen> {
  final ApiService _api = ApiService();
  final TextEditingController _controller = TextEditingController();
  final Map<MarketOutcome, TextEditingController> _sellControllers = {
    MarketOutcome.yes: TextEditingController(),
    MarketOutcome.no: TextEditingController(),
  };
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  late Item _item;
  bool _submitted = false;
  bool _submitting = false;
  bool _loadingHoldings = false;
  int? _submittedValue;
  String? _submitError;
  String? _selectedOutcome;
  List<_EventHoldingSummary> _myHoldings = const [];
  bool _isResolving = false;
  double _slippageTolerance = 0.05;
  bool _showAdvancedSettings = false;
  bool _enableCustomSlippage = false;
  StreamSubscription<Map<String, dynamic>>? _sseSub;

  List<PriceHistoryPoint> _priceHistory = [];
  bool _loadingHistory = false;

  @override
  void initState() {
    super.initState();
    _item = widget.item;
    _loadMyEventHoldings();
    _loadPriceHistory();

    // Connect to SSE and listen for price updates for this event
    SseService.instance.connect().then((_) {
      _sseSub = SseService.instance.priceStream.listen((data) {
        try {
          final type = data['type'] ?? '';
          if (type != 'price') return;
          final eventId =
              (data['eventId'] ?? data['id'] ?? '').toString().toLowerCase();
          if (eventId != _item.id.toLowerCase()) return;
          final priceYes = data['priceYes'] ?? data['price_yes'];
          final priceNo = data['priceNo'] ?? data['price_no'];
          final pYes = priceYes is num
              ? priceYes.toDouble()
              : double.tryParse(priceYes?.toString() ?? '');
          final pNo = priceNo is num
              ? priceNo.toDouble()
              : double.tryParse(priceNo?.toString() ?? '');
          debugPrint(
              'SSE price event matched: $eventId priceYes=$pYes priceNo=$pNo');
          if (pYes != null || pNo != null) {
            if (!mounted) return;
            setState(() {
              _item = _item.copyWith(
                priceYes: pYes ?? _item.priceYes,
                priceNo: pNo ?? _item.priceNo,
              );
              _priceHistory = [
                ..._priceHistory,
                PriceHistoryPoint(
                  eventId: _item.id,
                  priceYes: pYes ?? _item.priceYes,
                  priceNo: pNo ?? _item.priceNo,
                  timestamp: DateTime.now(),
                ),
              ];
            });
          }
        } catch (_) {}
      });
    });
  }

  @override
  void dispose() {
    _sseSub?.cancel();
    _controller.dispose();
    for (final controller in _sellControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  double _toDouble(dynamic value) {
    if (value is num) {
      return value.toDouble();
    }
    if (value is String) {
      return double.tryParse(value) ?? 0.0;
    }
    return 0.0;
  }

  Future<void> _loadPriceHistory() async {
    setState(() {
      _loadingHistory = true;
    });

    try {
      final history = await _api.fetchPriceHistory(_item.id);
      if (!mounted) return;
      setState(() {
        _priceHistory = history;
        _loadingHistory = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _priceHistory = const [];
        _loadingHistory = false;
      });
    }
  }

  Future<void> _loadMyEventHoldings() async {
    setState(() {
      _loadingHoldings = true;
    });

    try {
      final transactions = await _api.fetchTransactions();
      final byOutcome = <MarketOutcome, double>{
        MarketOutcome.yes: 0,
        MarketOutcome.no: 0,
      };

      for (final tx in transactions) {
        final eventId = (tx['eventId'] ?? tx['event_id'] ?? '').toString();
        if (eventId != _item.id) continue;

        final outcomeValue = tx['outcome'] ?? tx['Outcome'] ?? 1;
        final outcome = outcomeValue == 2 || outcomeValue.toString() == '2'
            ? MarketOutcome.no
            : MarketOutcome.yes;
        final amount = _toDouble(
          tx['shareAmount'] ?? tx['share_amount'] ?? tx['ShareAmount'],
        );

        // Transaction type: 1 = Buy (add), 2 = Sell (subtract). Default to Buy when parsing fails.
        final rawType = tx['type'] ?? tx['Type'] ?? tx['transactionType'] ?? tx['TransactionType'];
        int typeInt;
        if (rawType is int) {
          typeInt = rawType;
        } else if (rawType is String) {
          typeInt = int.tryParse(rawType) ?? 1;
        } else {
          typeInt = 1;
        }

        final signedAmount = typeInt == 2 ? -amount : amount;
        byOutcome[outcome] = (byOutcome[outcome] ?? 0) + signedAmount;
      }

      final rows = <_EventHoldingSummary>[];
      final yesAmount = byOutcome[MarketOutcome.yes] ?? 0;
      final noAmount = byOutcome[MarketOutcome.no] ?? 0;

      if (yesAmount > 0) {
        rows.add(_EventHoldingSummary(outcome: MarketOutcome.yes, shares: yesAmount));
      }
      if (noAmount > 0) {
        rows.add(_EventHoldingSummary(outcome: MarketOutcome.no, shares: noAmount));
      }

      if (!context.mounted) return;
      setState(() {
        _myHoldings = rows;
        _loadingHoldings = false;
      });
    } catch (_) {
      if (!context.mounted) return;
      setState(() {
        _myHoldings = const [];
        _loadingHoldings = false;
      });
    }
  }

  Future<void> _sellHolding(MarketOutcome outcome) async {
    final controller = _sellControllers[outcome]!;
    final shares = double.tryParse(controller.text.trim());

    if (shares == null || shares <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Enter a valid share amount greater than 0'),
          backgroundColor: Color(0xFFB00020),
        ),
      );
      return;
    }

    try {
      final expectedPrice = outcome == MarketOutcome.yes ? _item.priceYes : _item.priceNo;

      await _api.sellShares(
        eventId: _item.id,
        shares: shares,
        outcome: outcome == MarketOutcome.yes ? 'Yes' : 'No',
        expectedPrice: expectedPrice,
        slippageDelta: _slippageTolerance,
      );
      controller.clear();
      await _loadMyEventHoldings();
      if (!context.mounted) return;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        final m = ScaffoldMessenger.maybeOf(context);
        m?.showSnackBar(const SnackBar(content: Text('Sell request sent')));
      });
    } catch (e) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        final m = ScaffoldMessenger.maybeOf(context);
        m?.showSnackBar(SnackBar(content: Text('Sell failed: $e')));
      });
    }
  }

  Future<void> _submit() async {
    if (_item.isResolved) return;

    if (_selectedOutcome == null) {
      setState(() {
        _submitError = 'Select an outcome first';
        _submitted = false;
      });
      return;
    }

    if (_formKey.currentState?.validate() ?? false) {
      final value = int.parse(_controller.text);
      setState(() {
        _submitting = true;
        _submitted = false;
        _submittedValue = value;
        _submitError = null;
      });
      FocusScope.of(context).unfocus();

      try {
        final expectedPrice = _selectedOutcome == 'Yes' ? _item.priceYes : _item.priceNo;

        await _api.createAccountStake(
          id: _item.id,
          stake: value,
          outcome: _selectedOutcome!,
          expectedPrice: expectedPrice,
          slippageDelta: _enableCustomSlippage ? _slippageTolerance : null,
        );
        await _refreshMarket();
        if (!context.mounted) return;

        setState(() {
          _submitted = true;
          _submitting = false;
        });

        WidgetsBinding.instance.addPostFrameCallback((_) {
          final m = ScaffoldMessenger.maybeOf(context);
          m?.showSnackBar(
            SnackBar(
              content: Text('Stake submitted: $value'),
              backgroundColor: const Color(0xFF111827),
              behavior: SnackBarBehavior.floating,
              duration: const Duration(seconds: 2),
            ),
          );
        });
      } catch (error) {
        if (!context.mounted) return;

        setState(() {
          _submitting = false;
          _submitError = error.toString();
        });
      }
    }
  }

  Future<void> _resolveMarket(int outcome) async {
    setState(() {
      _isResolving = true;
    });

    try {
      await _api.resolveEvent(id: _item.id, outcome: outcome);
      await _refreshMarket();
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Market resolved as ${outcome == 1 ? "YES" : "NO"}!'),
          backgroundColor: const Color(0xFF166534),
          behavior: SnackBarBehavior.floating,
        ),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Resolve error: $error'),
          backgroundColor: const Color(0xFF7F1D1D),
          behavior: SnackBarBehavior.floating,
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _isResolving = false;
        });
      }
    }
  }

  Future<void> _refreshMarket() async {
    final previousPotSize = _item.potSize;

    // Bets are processed asynchronously through RabbitMQ. Poll briefly so the
    // screen reflects the Catalog update as soon as it is persisted.
    for (var attempt = 0; attempt < 5; attempt++) {
      await Future<void>.delayed(const Duration(milliseconds: 500));
      final updatedItem = await _api.fetchItem(_item.id);
      if (!context.mounted) return;

      setState(() {
        _item = updatedItem;
      });

      if (updatedItem.potSize != previousPotSize || updatedItem.isResolved) {
        return;
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final item = _item;

    // Uzimamo ulogovanog korisnika i proveravamo da li je on owner eventa
    final currentUserId = AuthService.instance.userId;
    final isOwner = currentUserId != null && currentUserId == item.ownerId;

    return Scaffold(
      backgroundColor: const Color(0xFF070A0F),
      appBar: AppBar(
        backgroundColor: const Color(0xFF070A0F),
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Colors.white),
          onPressed: () => Navigator.pop(context),
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.share_outlined, color: Colors.white),
            onPressed: () {},
          ),
          IconButton(
            icon: const Icon(Icons.more_vert, color: Colors.white),
            onPressed: () {},
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _Panel(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    item.title,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 22,
                      fontWeight: FontWeight.w800,
                      height: 1.18,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            _OutcomePanel(
              item: item,
              selectedOutcome: _selectedOutcome,
              onOutcomeSelected: (outcome) {
                if (item.isResolved) return;
                setState(() {
                  _selectedOutcome = outcome;
                  _submitError = null;
                  _submitted = false;
                });
              },
            ),
            const SizedBox(height: 12),
            _PriceHistoryPanel(
              loading: _loadingHistory,
              history: _priceHistory,
            ),
            const SizedBox(height: 12),

            // Ako je korisnik vlasnik i event još nije završen, prikazujemo Owner Tools
            if (isOwner && !item.isResolved) ...[
              _OwnerResolvePanel(
                isResolving: _isResolving,
                onResolve: _resolveMarket,
              ),
              const SizedBox(height: 12),
            ],

            if (item.isResolved)
              _Panel(
                child: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: const Color(0xFF1F2937),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Column(
                    children: [
                      const Icon(Icons.lock, color: Colors.white54, size: 28),
                      const SizedBox(height: 6),
                      const Text(
                        'Event Market Resolved',
                        style: TextStyle(
                          color: Colors.white,
                          fontSize: 16,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Winning Outcome: ${item.outcome == 1 ? "YES" : "NO"}',
                        style: TextStyle(
                          color: item.outcome == 1
                              ? const Color(0xFF00A3FF)
                              : const Color(0xFFEF4444),
                          fontSize: 14,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ],
                  ),
                ),
              )
            else
              _TradePanel(
                formKey: _formKey,
                controller: _controller,
                submitted: _submitted,
                submitting: _submitting,
                submittedValue: _submittedValue,
                submitError: _submitError,
                onSubmit: _submit,
                slippageTolerance: _slippageTolerance,
                onSlippageChanged: (value) {
                  setState(() {
                    _slippageTolerance = value;
                  });
                },
                showAdvancedSettings: _showAdvancedSettings,
                onAdvancedSettingsToggled: (value) {
                  setState(() => _showAdvancedSettings = value);
                },
                enableCustomSlippage: _enableCustomSlippage,
                onCustomSlippageToggled: (value) {
                  setState(() => _enableCustomSlippage = value ?? false );
                },
              ),
            const SizedBox(height: 12),
            _HoldingPanel(
              loading: _loadingHoldings,
              holdings: _myHoldings,
              sellControllers: _sellControllers,
              onSell: _sellHolding,
            ),
          ],
        ),
      ),
    );
  }
}

class _OwnerResolvePanel extends StatelessWidget {
  final bool isResolving;
  final Function(int outcome) onResolve;

  const _OwnerResolvePanel({
    required this.isResolving,
    required this.onResolve,
  });

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.admin_panel_settings,
                  color: Color(0xFFF59E0B), size: 20),
              SizedBox(width: 6),
              Text(
                'Owner Tools: Resolve Market',
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 15,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          const Text(
            'As the owner of this market, declare the final winning outcome to distribute payouts:',
            style: TextStyle(color: Colors.white60, fontSize: 12),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: ElevatedButton(
                  onPressed: isResolving ? null : () => onResolve(1),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF00A3FF),
                    foregroundColor: Colors.white,
                    disabledBackgroundColor: const Color(0xFF1E3A8A),
                  ),
                  child: Text(isResolving ? 'Resolving...' : 'Resolve YES'),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: ElevatedButton(
                  onPressed: isResolving ? null : () => onResolve(2),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFFEF4444),
                    foregroundColor: Colors.white,
                    disabledBackgroundColor: const Color(0xFF7F1D1D),
                  ),
                  child: Text(isResolving ? 'Resolving...' : 'Resolve NO'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _OutcomePanel extends StatelessWidget {
  final Item item;
  final String? selectedOutcome;
  final ValueChanged<String> onOutcomeSelected;

  const _OutcomePanel({
    required this.item,
    required this.selectedOutcome,
    required this.onOutcomeSelected,
  });

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Outcome prices',
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 12),
          Column(
            children: [
              _OutcomeRow(
                label: 'Yes',
                price: item.priceYes,
                color: const Color(0xFF00A3FF),
                selected: item.isResolved ? item.outcome == 1 : selectedOutcome == 'Yes',
                onTap: () => onOutcomeSelected('Yes'),
              ),
              const SizedBox(height: 8),
              _OutcomeRow(
                label: 'No',
                price: item.priceNo,
                color: const Color(0xFFEF4444),
                selected: item.isResolved ? item.outcome == 2 : selectedOutcome == 'No',
                onTap: () => onOutcomeSelected('No'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _OutcomeRow extends StatelessWidget {
  final String label;
  final double price;
  final Color color;
  final bool selected;
  final VoidCallback onTap;

  const _OutcomeRow({
    required this.label,
    required this.price,
    required this.color,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: selected
              ? Color.fromRGBO((argbFromColor(color) >> 16) & 0xFF,
                  (argbFromColor(color) >> 8) & 0xFF, argbFromColor(color) & 0xFF, 0.12)
              : const Color(0xFF111827),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: selected ? color : const Color(0xFF1F2937),
            width: selected ? 1.5 : 1,
          ),
        ),
        child: Row(
          children: [
            Container(
              width: 10,
              height: 10,
              decoration: BoxDecoration(
                color: color,
                shape: BoxShape.circle,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                label,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            Text(
              '£${price.toStringAsFixed(2)}',
              style: TextStyle(
                color: color,
                fontSize: 18,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _PriceHistoryPanel extends StatelessWidget {
  final bool loading;
  final List<PriceHistoryPoint> history;

  const _PriceHistoryPanel({required this.loading, required this.history});

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Price history',
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 180,
            child: loading
                ? const Center(
                    child: CircularProgressIndicator(
                      color: Color(0xFF00A3FF),
                      strokeWidth: 2,
                    ),
                  )
                : history.length < 2
                    ? const Center(
                        child: Text(
                          'Not enough data yet',
                          style: TextStyle(color: Colors.white60),
                        ),
                      )
                    : LineChart(_buildChartData(history)),
          ),
          const SizedBox(height: 8),
          const Row(
            children: [
              _LegendDot(color: Color(0xFF00A3FF), label: 'Yes'),
              SizedBox(width: 16),
              _LegendDot(color: Color(0xFFEF4444), label: 'No'),
            ],
          ),
        ],
      ),
    );
  }

  LineChartData _buildChartData(List<PriceHistoryPoint> history) {
    final yesSpots = <FlSpot>[];
    final noSpots = <FlSpot>[];

    for (var i = 0; i < history.length; i++) {
      yesSpots.add(FlSpot(i.toDouble(), history[i].priceYes));
      noSpots.add(FlSpot(i.toDouble(), history[i].priceNo));
    }

    return LineChartData(
      gridData: FlGridData(
        show: true,
        drawVerticalLine: false,
        horizontalInterval: 0.25,
        getDrawingHorizontalLine: (value) => const FlLine(
          color: Color(0xFF1F2937),
          strokeWidth: 1,
        ),
      ),
      titlesData: FlTitlesData(
        topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
        rightTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
        bottomTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
        leftTitles: AxisTitles(
          sideTitles: SideTitles(
            showTitles: true,
            reservedSize: 36,
            interval: 0.25,
            getTitlesWidget: (value, meta) => Text(
              '£${value.toStringAsFixed(2)}',
              style: const TextStyle(color: Colors.white38, fontSize: 10),
            ),
          ),
        ),
      ),
      borderData: FlBorderData(show: false),
      minY: 0,
      maxY: 1,
      lineTouchData: LineTouchData(
        touchTooltipData: LineTouchTooltipData(
          getTooltipItems: (spots) => spots.map((s) {
            final isYes = s.barIndex == 0;
            return LineTooltipItem(
              '${isYes ? "Yes" : "No"}: £${s.y.toStringAsFixed(2)}',
              TextStyle(
                color: isYes ? const Color(0xFF00A3FF) : const Color(0xFFEF4444),
                fontWeight: FontWeight.w700,
                fontSize: 12,
              ),
            );
          }).toList(),
        ),
      ),
      lineBarsData: [
        LineChartBarData(
          spots: yesSpots,
          isCurved: true,
          color: const Color(0xFF00A3FF),
          barWidth: 2,
          dotData: const FlDotData(show: false),
        ),
        LineChartBarData(
          spots: noSpots,
          isCurved: true,
          color: const Color(0xFFEF4444),
          barWidth: 2,
          dotData: const FlDotData(show: false),
        ),
      ],
    );
  }
}

class _LegendDot extends StatelessWidget {
  final Color color;
  final String label;

  const _LegendDot({required this.color, required this.label});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: 6),
        Text(label, style: const TextStyle(color: Colors.white60, fontSize: 12)),
      ],
    );
  }
}

class _TradePanel extends StatelessWidget {
  final GlobalKey<FormState> formKey;
  final TextEditingController controller;
  final bool submitted;
  final bool submitting;
  final int? submittedValue;
  final String? submitError;
  final VoidCallback onSubmit;
  final double slippageTolerance;
  final ValueChanged<double> onSlippageChanged;
  
  // Nova polja
  final bool showAdvancedSettings;
  final ValueChanged<bool> onAdvancedSettingsToggled;
  final bool enableCustomSlippage;
  final ValueChanged<bool?> onCustomSlippageToggled;

  const _TradePanel({
    required this.formKey,
    required this.controller,
    required this.submitted,
    required this.submitting,
    required this.submittedValue,
    required this.submitError,
    required this.onSubmit,
    required this.slippageTolerance,
    required this.onSlippageChanged,
    required this.showAdvancedSettings,
    required this.onAdvancedSettingsToggled,
    required this.enableCustomSlippage,
    required this.onCustomSlippageToggled,
  });

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Place order',
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 12),
          Form(
            key: formKey,
            child: TextFormField(
              controller: controller,
              keyboardType: TextInputType.number,
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp(r'^\d*')),
              ],
              style: const TextStyle(color: Colors.white, fontSize: 16),
              decoration: InputDecoration(
                hintText: 'Shares',
                hintStyle: const TextStyle(color: Colors.white38),
                filled: true,
                fillColor: const Color(0xFF111827),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: BorderSide.none,
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: Color(0xFF00A3FF), width: 1.5),
                ),
                errorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: Color(0xFFEF4444)),
                ),
                focusedErrorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: Color(0xFFEF4444)),
                ),
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: 14,
                  vertical: 13,
                ),
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) {
                  return 'Enter share amount';
                }
                final amount = int.tryParse(value);
                if (amount == null || amount <= 0) {
                  return 'Use a positive whole number';
                }
                return null;
              },
            ),
          ),
          
          const SizedBox(height: 8),

          // Advanced Settings Toggle
          InkWell(
            onTap: () => onAdvancedSettingsToggled(!showAdvancedSettings),
            borderRadius: BorderRadius.circular(6),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 8.0, horizontal: 4.0),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    showAdvancedSettings ? Icons.keyboard_arrow_up : Icons.keyboard_arrow_down,
                    color: Colors.white60,
                    size: 20,
                  ),
                  const SizedBox(width: 6),
                  const Text(
                    'Advanced Settings',
                    style: TextStyle(color: Colors.white60, fontSize: 13, fontWeight: FontWeight.w600),
                  ),
                ],
              ),
            ),
          ),

          // Expanded Content
          if (showAdvancedSettings) ...[
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFF111827),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: const Color(0xFF1F2937)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      SizedBox(
                        width: 24,
                        height: 24,
                        child: Checkbox(
                          value: enableCustomSlippage,
                          onChanged: onCustomSlippageToggled,
                          activeColor: const Color(0xFF00A3FF),
                          side: const BorderSide(color: Colors.white54),
                        ),
                      ),
                      const SizedBox(width: 10),
                      GestureDetector(
                        onTap: () => onCustomSlippageToggled(!enableCustomSlippage),
                        child: const Text(
                          'Custom Slippage Tolerance',
                          style: TextStyle(color: Colors.white, fontSize: 14),
                        ),
                      ),
                    ],
                  ),
                  if (enableCustomSlippage) ...[
                    const SizedBox(height: 16),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        const Text(
                          'Tolerance limit',
                          style: TextStyle(color: Colors.white70, fontSize: 13),
                        ),
                        Text(
                          '${(slippageTolerance * 100).toStringAsFixed(0)}%',
                          style: const TextStyle(
                            color: Color(0xFF00A3FF),
                            fontSize: 13,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                    SliderTheme(
                      data: SliderTheme.of(context).copyWith(
                        trackHeight: 4,
                        thumbShape: const RoundSliderThumbShape(enabledThumbRadius: 6),
                        overlayShape: const RoundSliderOverlayShape(overlayRadius: 14),
                      ),
                      child: Slider(
                        value: slippageTolerance,
                        min: 0.01,
                        max: 0.25,
                        divisions: 24,
                        activeColor: const Color(0xFF00A3FF),
                        inactiveColor: const Color(0xFF1F2937),
                        onChanged: onSlippageChanged,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ],

          const SizedBox(height: 16),
          
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: submitting ? null : onSubmit,
              style: FilledButton.styleFrom(
                backgroundColor: const Color(0xFF00A3FF),
                foregroundColor: Colors.white,
                disabledBackgroundColor: const Color(0xFF263244),
                disabledForegroundColor: Colors.white54,
                padding: const EdgeInsets.symmetric(vertical: 14),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(8),
                ),
              ),
              child: Text(submitting ? 'Submitting...' : 'Preview trade'),
            ),
          ),
          if (submitError != null) ...[
            const SizedBox(height: 12),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFF3A1010),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: const Color(0xFF7F1D1D)),
              ),
              child: Text(
                submitError!,
                style: const TextStyle(
                  color: Color(0xFFFCA5A5),
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ],
          if (submitted && submittedValue != null) ...[
            const SizedBox(height: 12),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFF052E1A),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: const Color(0xFF166534)),
              ),
              child: Text(
                'Stake submitted: $submittedValue',
                style: const TextStyle(
                  color: Color(0xFF86EFAC),
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _EventHoldingSummary {
  final MarketOutcome outcome;
  final double shares;

  const _EventHoldingSummary({
    required this.outcome,
    required this.shares,
  });

  String get label => outcome == MarketOutcome.yes ? 'YES' : 'NO';
}

class _HoldingPanel extends StatelessWidget {
  final bool loading;
  final List<_EventHoldingSummary> holdings;
  final Map<MarketOutcome, TextEditingController> sellControllers;
  final Future<void> Function(MarketOutcome outcome) onSell;

  const _HoldingPanel({
    required this.loading,
    required this.holdings,
    required this.sellControllers,
    required this.onSell,
  });

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Your shares on this event',
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 12),
          if (loading)
            const Center(
              child: Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: CircularProgressIndicator(
                  color: Color(0xFF00A3FF),
                  strokeWidth: 2,
                ),
              ),
            )
          else if (holdings.isEmpty)
            const Text(
              'You do not currently hold shares for this event.',
              style: TextStyle(color: Colors.white60),
            )
          else
            ...holdings.map((holding) {
              final controller = sellControllers[holding.outcome]!;
              return Container(
                margin: const EdgeInsets.only(bottom: 10),
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: const Color(0xFF111827),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Outcome: ${holding.label}',
                            style: const TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${holding.shares.toStringAsFixed(2)} shares',
                            style: const TextStyle(
                              color: Color(0xFF00A3FF),
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    SizedBox(
                      width: 96,
                      child: TextFormField(
                        controller: controller,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        style: const TextStyle(color: Colors.white),
                        decoration: InputDecoration(
                          hintText: 'Shares',
                          hintStyle: const TextStyle(color: Colors.white38),
                          filled: true,
                          fillColor: const Color(0xFF0D1320),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(8),
                            borderSide: BorderSide.none,
                          ),
                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 10,
                            vertical: 10,
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    ElevatedButton(
                      onPressed: () => onSell(holding.outcome),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFFFF7A59),
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 14,
                        ),
                      ),
                      child: const Text('Sell'),
                    ),
                  ],
                ),
              );
            }),
        ],
      ),
    );
  }
}

class _Panel extends StatelessWidget {
  final Widget child;

  const _Panel({required this.child});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFF0D1320),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: const Color(0xFF1F2937)),
      ),
      child: child,
    );
  }
}