import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

class AuthService extends ChangeNotifier {
  AuthService._();

  static final AuthService instance = AuthService._();

  static const _tokenKey = 'auth_token';

  String? _token;

  String? get token => _token;

  bool get isLoggedIn => _token != null;

  Future<void> init() async {
    final prefs = await SharedPreferences.getInstance();
    _token = prefs.getString(_tokenKey);
    notifyListeners();
  }

  Future<void> login(String token) async {
    _token = token;
    notifyListeners();

    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, token);
  }

  Future<void> logout() async {
    _token = null;
    notifyListeners();

    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
  }
}
