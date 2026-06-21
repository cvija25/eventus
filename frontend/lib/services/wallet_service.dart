import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

class WalletService extends ChangeNotifier {
  WalletService._();

  static final WalletService instance = WalletService._();

  static const _balanceKey = 'wallet_balance';

  double _balance = 0.0;

  double get balance => _balance;

  Future<void> init() async {
    final prefs = await SharedPreferences.getInstance();
    _balance = prefs.getDouble(_balanceKey) ?? 0.0;
    notifyListeners();
  }

  Future<void> setBalance(double value) async {
    _balance = value;
    notifyListeners();
    final prefs = await SharedPreferences.getInstance();
    await prefs.setDouble(_balanceKey, _balance);
  }

  Future<void> deposit(double amount) async {
    if (amount <= 0) return;
    await setBalance(_balance + amount);
  }

  Future<void> withdraw(double amount) async {
    if (amount <= 0) return;
    final newBal = (_balance - amount).clamp(0.0, double.infinity);
    await setBalance(newBal);
  }
}
