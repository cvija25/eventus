import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

class AuthService extends ChangeNotifier {
  AuthService._();

  static final AuthService instance = AuthService._();

  static const _tokenKey = 'auth_token';

  String? _token;

  String? get token => _token;

  bool get isLoggedIn => _token != null;

  String? get currentUserName {
    final token = _token;
    if (token == null || token.split('.').length < 2) {
      return null;
    }

    try {
      final payload = token.split('.')[1];
      final normalized = payload
          .padRight(payload.length + ((4 - payload.length % 4) % 4), '=');
      final decoded = utf8.decode(base64Url.decode(normalized));
      final data = jsonDecode(decoded) as Map<String, dynamic>;
      final name = data['name'];
      if (name is String && name.trim().isNotEmpty) {
        return name;
      }
      final email = data['email'];
      if (email is String && email.contains('@')) {
        return email.split('@').first;
      }
    } catch (_) {
      return null;
    }

    return null;
  }

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
