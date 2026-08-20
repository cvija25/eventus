import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../services/auth_service.dart';

enum MarketOutcome { yes, no }

class ShareHolding {
  const ShareHolding({
    required this.eventId,
    required this.userId,
    required this.shareAmount,
    required this.outcome,
  });

  final String eventId;
  final String userId;
  final double shareAmount;
  final MarketOutcome outcome;

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
      if (!mounted) return;
      setState(() {
        _balance = balance;
        _isLoading = false;
      });
    } catch (e) {
      if (!mounted) return;
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
      if (!mounted) return;

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

              if (eventId.isEmpty || userId.isEmpty) {
                return null;
              }

              return ShareHolding(
                eventId: eventId,
                userId: userId,
                shareAmount: shareAmount,
                outcome: outcome,
              );
            })
            .whereType<ShareHolding>()
            .toList();
        _transactionsLoading = false;
      });
    } catch (e) {
      if (!mounted) return;
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
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Deposit successful')),
      );
    } catch (e) {
      if (!mounted) return;
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
                      ..._shareHoldings.map((holding) {
                        return Container(
                          margin: const EdgeInsets.only(bottom: 10),
                          padding: const EdgeInsets.all(14),
                          decoration: BoxDecoration(
                            color: const Color(0xFF111B2B),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      'Event ${holding.eventId.substring(0, 8)}',
                                      style: const TextStyle(
                                        color: Colors.white,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      'Outcome: ${holding.outcomeLabel}',
                                      style: const TextStyle(
                                        color: Colors.white70,
                                        fontSize: 12,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              Column(
                                crossAxisAlignment: CrossAxisAlignment.end,
                                children: [
                                  Text(
                                    '${holding.shareAmount.toStringAsFixed(2)} shares',
                                    style: const TextStyle(
                                      color: Color(0xFF00A3FF),
                                      fontWeight: FontWeight.bold,
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
                            ],
                          ),
                        );
                      }),
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
