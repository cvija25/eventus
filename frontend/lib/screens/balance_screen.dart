import 'package:flutter/material.dart';
import '../services/wallet_service.dart';
import '../services/api_service.dart';

class BalanceScreen extends StatefulWidget {
  const BalanceScreen({super.key});

  @override
  State<BalanceScreen> createState() => _BalanceScreenState();
}

class _BalanceScreenState extends State<BalanceScreen> {
  final _amountController = TextEditingController();

  @override
  void dispose() {
    _amountController.dispose();
    super.dispose();
  }

  Future<void> _showDepositDialog() async {
    _amountController.text = '';
    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Deposit'),
        content: TextField(
          controller: _amountController,
          keyboardType:
              const TextInputType.numberWithOptions(decimal: true),
          decoration: const InputDecoration(hintText: 'Amount'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Cancel'),
          ),
          ElevatedButton(
            onPressed: () async {
              final text = _amountController.text.trim();
              final value = double.tryParse(text);
              if (value == null || value <= 0) return;
              // Call backend deposit endpoint
              try {
                await ApiService().depositToAccount(stake: value);
                // Update local wallet after successful backend call
                await WalletService.instance.deposit(value);
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Deposit successful')),
                  );
                }
              } catch (e) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(content: Text('Deposit failed: $e')),
                  );
                }
              } finally {
                Navigator.of(context).pop();
              }
            },
            child: const Text('Deposit'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Wallet'),
        backgroundColor: const Color(0xFF070A0F),
      ),
      body: Padding(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ListenableBuilder(
              listenable: WalletService.instance,
              builder: (context, _) {
                final bal = WalletService.instance.balance;
                return Row(
                  children: [
                    const Text(
                      'Balance:',
                      style: TextStyle(
                        fontSize: 20,
                      ),
                    ),
                    const SizedBox(width: 8),
                    Container(
                      width: 22,
                      height: 22,
                      decoration: BoxDecoration(
                        color: const Color(0xFF00A3FF),
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
                    const SizedBox(width: 6),
                    Text(
                      bal.toStringAsFixed(2),
                      style: const TextStyle(
                        fontSize: 28,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                );
              },
            ),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: _showDepositDialog,
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF00A3FF),
              ),
              child: const Text('Add / Deposit Money'),
            ),
          ],
        ),
      ),
    );
  }
}
