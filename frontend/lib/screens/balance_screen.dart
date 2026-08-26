import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../services/auth_service.dart';
import 'detail_screen.dart';

enum MarketOutcome { yes, no }

class ShareHolding {
  const ShareHolding({
    required this.eventId,
    required this.userId,
    required this.shareAmount,
    required this.outcome,
    required this.type,
  });

  final String eventId;
  final String userId;
  final double shareAmount;
  final MarketOutcome outcome;
  final int type;

  String get outcomeLabel => outcome == MarketOutcome.yes ? 'YES' : 'NO';
}

class BalanceScreen extends StatefulWidget {
  const BalanceScreen({super.key});

  @override
  State<BalanceScreen> createState() => _BalanceScreenState();
}

class _BalanceScreenState extends State<BalanceScreen> {
  final _amountController = TextEditingController();
  final _api = ApiService();
  List<ShareHolding> _shareHoldings = const [];
  final Set<String> _expandedEventIds = <String>{};
  double _balance = 0.0;
  bool _isLoading = true;
  bool _transactionsLoading = true;
  String? _errorMessage;
  String? _transactionsError;

  String get _accountName =>
      AuthService.instance.currentUserName ?? 'Guest User';

  @override
  void initState() {
    super.initState();
    _loadBalance();
    _loadTransactions();
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

  Future<void> _loadBalance() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final balance = await _api.fetchBalance();
      if (!context.mounted) return;
      setState(() {
        _balance = balance;
        _isLoading = false;
      });
    } catch (e) {
      if (!context.mounted) return;
      setState(() {
        _errorMessage = e.toString();
        _isLoading = false;
      });
    }
  }

  Future<void> _loadTransactions() async {
    setState(() {
      _transactionsLoading = true;
      _transactionsError = null;
    });

    try {
      final transactions = await _api.fetchTransactions();
      if (!context.mounted) return;

      setState(() {
        _shareHoldings = transactions
            .map((item) {
              final eventId =
                  (item['eventId'] ?? item['event_id'] ?? '').toString();
              final userId =
                  (item['userId'] ?? item['user_id'] ?? '').toString();
              final shareAmount = _toDouble(
                item['shareAmount'] ??
                    item['share_amount'] ??
                    item['ShareAmount'],
              );
              final outcomeValue = item['outcome'] ?? item['Outcome'] ?? 1;
              final outcome =
                  outcomeValue == 2 || outcomeValue.toString() == '2'
                      ? MarketOutcome.no
                      : MarketOutcome.yes;

                      final rawType = item['type'] ?? item['Type'] ?? item['transactionType'] ?? item['TransactionType'];
                      int typeInt;
                      if (rawType is int) {
                        typeInt = rawType;
                      } else if (rawType is String) {
                        typeInt = int.tryParse(rawType) ?? 1;
                      } else {
                        typeInt = 1;
                      }

              if (eventId.isEmpty || userId.isEmpty) {
                return null;
              }

              return ShareHolding(
                eventId: eventId,
                userId: userId,
                shareAmount: shareAmount,
                outcome: outcome,
                type: typeInt,
              );
            })
            .whereType<ShareHolding>()
            .toList();

        _transactionsLoading = false;
      });
    } catch (e) {
      if (!context.mounted) return;
      setState(() {
        _shareHoldings = const [];
        _transactionsLoading = false;
        _transactionsError = e.toString();
      });
    }
  }

  Future<void> _depositNow() async {
    final text = _amountController.text.trim();
    final value = double.tryParse(text);

    if (value == null || value <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Enter a valid amount greater than 0'),
          backgroundColor: Color(0xFFB00020),
        ),
      );
      return;
    }

    try {
      await _api.depositToAccount(amount: value);
      _amountController.clear();
      await _loadBalance();
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Deposit successful')),
      );
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Deposit failed: $e')),
      );
    }
  }

  @override
  void dispose() {
    _amountController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isLoggedIn = AuthService.instance.isLoggedIn;
    final groupedHoldings = <String, List<ShareHolding>>{};
    for (final holding in _shareHoldings) {
      groupedHoldings.putIfAbsent(holding.eventId, () => <ShareHolding>[]).add(holding);
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile'),
        backgroundColor: const Color(0xFF070A0F),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: const Color(0xFF0D1320),
                  borderRadius: BorderRadius.circular(18),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Account',
                      style: TextStyle(
                        color: Colors.white70,
                        fontSize: 14,
                        letterSpacing: 0.6,
                      ),
                    ),
                    const SizedBox(height: 16),
                    Row(
                      children: [
                        CircleAvatar(
                          radius: 28,
                          backgroundColor: const Color(0xFF00A3FF),
                          child: Text(
                            _accountName.isNotEmpty
                                ? _accountName.substring(0, 1).toUpperCase()
                                : 'G',
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 24,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                        const SizedBox(width: 16),
                        Expanded(
                          child: Text(
                            _accountName,
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 24,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 24),
                    const Text(
                      'Balance',
                      style: TextStyle(
                        color: Colors.white70,
                        fontSize: 14,
                      ),
                    ),
                    const SizedBox(height: 8),
                    if (_isLoading)
                      const Center(
                        child: Padding(
                          padding: EdgeInsets.symmetric(vertical: 12),
                          child: CircularProgressIndicator(
                            color: Color(0xFF00A3FF),
                          ),
                        ),
                      )
                    else if (_errorMessage != null)
                      Text(
                        _errorMessage!,
                        style: const TextStyle(color: Colors.redAccent),
                      )
                    else
                      Row(
                        children: [
                          Container(
                            width: 22,
                            height: 22,
                            decoration: const BoxDecoration(
                              color: Color(0xFF00A3FF),
                              shape: BoxShape.circle,
                            ),
                            alignment: Alignment.center,
                            child: const Text(
                              'C',
                              style: TextStyle(
                                color: Colors.white,
                                fontSize: 14,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text(
                            _balance.toStringAsFixed(2),
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 30,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ],
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 28),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: const Color(0xFF0D1320),
                  borderRadius: BorderRadius.circular(18),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Transactions',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 12),
                    const Text(
                      'Current shares',
                      style: TextStyle(
                        color: Colors.white70,
                        fontSize: 14,
                      ),
                    ),
                    const SizedBox(height: 12),
                    if (_transactionsLoading)
                      const Center(
                        child: Padding(
                          padding: EdgeInsets.symmetric(vertical: 8),
                          child: CircularProgressIndicator(
                            color: Color(0xFF00A3FF),
                            strokeWidth: 2,
                          ),
                        ),
                      )
                    else if (_shareHoldings.isEmpty)
                      Text(
                        _transactionsError ?? 'No transactions yet.',
                        style: const TextStyle(color: Colors.white60),
                      )
                    else
                      Column(
                        children: groupedHoldings.entries.map((entry) {
                          final eventId = entry.key;
                          final holdings = entry.value;
                          final totalShares = holdings.fold<double>(
                            0,
                            (sum, holding) => sum + (holding.type == 2 ? -holding.shareAmount : holding.shareAmount),
                          );
                          final expanded = _expandedEventIds.contains(eventId);

                          return Container(
                            margin: const EdgeInsets.only(bottom: 10),
                            padding: const EdgeInsets.symmetric(
                              horizontal: 12,
                              vertical: 4,
                            ),
                            decoration: BoxDecoration(
                              color: const Color(0xFF111B2B),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: Theme(
                              data: Theme.of(context).copyWith(
                                dividerColor: Colors.transparent,
                              ),
                              child: ExpansionTile(
                                tilePadding: EdgeInsets.zero,
                                childrenPadding: const EdgeInsets.only(
                                  bottom: 12,
                                ),
                                initiallyExpanded: expanded,
                                onExpansionChanged: (value) {
                                  setState(() {
                                    if (value) {
                                      _expandedEventIds.add(eventId);
                                    } else {
                                      _expandedEventIds.remove(eventId);
                                    }
                                  });
                                },
                                title: GestureDetector(
                                  onTap: () async {
                                    try {
                                      final item = await _api.fetchItem(eventId);
                                      if (!context.mounted) return;
                                      await Navigator.push(
                                        context,
                                        MaterialPageRoute(
                                          builder: (_) => DetailScreen(item: item),
                                        ),
                                      );
                                      await _loadTransactions();
                                      await _loadBalance();
                                    } catch (e) {
                                      if (!context.mounted) return;
                                      ScaffoldMessenger.of(context).showSnackBar(
                                        SnackBar(
                                          content: Text('Could not open event: $e'),
                                        ),
                                      );
                                    }
                                  },
                                  child: Row(
                                    children: [
                                      Expanded(
                                        child: Text(
                                          'Event ${eventId.substring(0, 8)}',
                                          style: const TextStyle(
                                            color: Colors.white,
                                            fontWeight: FontWeight.w600,
                                          ),
                                        ),
                                      ),
                                      const Icon(
                                        Icons.open_in_new,
                                        size: 16,
                                        color: Colors.white70,
                                      ),
                                    ],
                                  ),
                                ),
                                trailing: Icon(
                                  expanded
                                      ? Icons.keyboard_arrow_up
                                      : Icons.keyboard_arrow_down,
                                  color: Colors.white70,
                                ),
                                children: [
                                  Padding(
                                    padding: const EdgeInsets.symmetric(
                                      horizontal: 8,
                                      vertical: 4,
                                    ),
                                    child: Row(
                                      mainAxisAlignment:
                                          MainAxisAlignment.spaceBetween,
                                      children: [
                                        const Text(
                                          'Total shares',
                                          style: TextStyle(
                                            color: Colors.white60,
                                            fontSize: 12,
                                          ),
                                        ),
                                        Text(
                                          totalShares.toStringAsFixed(2),
                                          style: const TextStyle(
                                            color: Color(0xFF00A3FF),
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  ...holdings.map((holding) {
                                    return Container(
                                      margin: const EdgeInsets.only(bottom: 6),
                                      padding: const EdgeInsets.all(10),
                                      decoration: BoxDecoration(
                                        color: const Color(0xFF0D1320),
                                        borderRadius: BorderRadius.circular(10),
                                      ),
                                      child: Row(
                                        children: [
                                          Expanded(
                                            child: Column(
                                              crossAxisAlignment:
                                                  CrossAxisAlignment.start,
                                              children: [
                                                Text(
                                                  'Outcome: ${holding.outcomeLabel}',
                                                  style: const TextStyle(
                                                    color: Colors.white,
                                                    fontWeight: FontWeight.w600,
                                                  ),
                                                ),
                                                const SizedBox(height: 4),
                                                Text(
                                                  'User ${holding.userId.substring(0, 8)}',
                                                  style: const TextStyle(
                                                    color: Colors.white38,
                                                    fontSize: 11,
                                                  ),
                                                ),
                                              ],
                                            ),
                                          ),
                                          Text(
                                            holding.shareAmount.toStringAsFixed(2),
                                            style: TextStyle(
                                              color: holding.type == 2 ? Colors.redAccent : const Color(0xFF00A3FF),
                                              fontWeight: FontWeight.bold,
                                            ),
                                          ),
                                        ],
                                      ),
                                    );
                                  }),
                                ],
                              ),
                            ),
                          );
                        }).toList(),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 28),
              Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      controller: _amountController,
                      enabled: isLoggedIn,
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      style: const TextStyle(color: Colors.white),
                      decoration: InputDecoration(
                        hintText:
                            isLoggedIn ? 'Enter amount' : 'Log in to deposit',
                        hintStyle: const TextStyle(color: Colors.white38),
                        filled: true,
                        fillColor: const Color(0xFF0D1320),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                          borderSide: BorderSide.none,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  ElevatedButton(
                    onPressed: isLoggedIn && !_isLoading ? _depositNow : null,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF00A3FF),
                      padding: const EdgeInsets.symmetric(
                        horizontal: 18,
                        vertical: 16,
                      ),
                    ),
                    child: const Text('Deposit now'),
                  ),
                ],
              ),
              if (!isLoggedIn) ...[
                const SizedBox(height: 12),
                const Text(
                  'Log in to deposit funds into your account.',
                  style: TextStyle(color: Colors.white60),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
