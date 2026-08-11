import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import '../models/item.dart';
import 'auth_service.dart';

class ApiService {
  static String get _host =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android
          ? '10.0.2.2'
          : 'localhost';

static const int _gatewayPort = 1234;

static String get _catalogUrl =>
    'http://$_host:$_gatewayPort/catalog/api/v1/catalog/events';

  static String get _identityUrl => 'http://$_host:$_gatewayPort/identity/api/v1/identity';

  Map<String, String> get _authHeaders {
    final token = AuthService.instance.token;
    return {
      'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };
  }

static String get _accountUrl => 'http://$_host:$_gatewayPort/account/api/v1/account/bet';
  static String get _depositUrl => 'http://$_host:$_gatewayPort/account/api/v1/account/deposit';
Future<List<Item>> fetchItems() async {
    final uri = Uri.parse(_catalogUrl);

    try {
      final response = await http.get(uri).timeout(const Duration(seconds: 10));

      if (response.statusCode == 200) {
        final decoded = jsonDecode(response.body);
        if (decoded is! List) {
          throw Exception('Expected a list of events from $uri');
        }

        return decoded
            .map((json) => Item.fromJson(json as Map<String, dynamic>))
            .toList();
      }

      throw Exception('GET $uri failed with status ${response.statusCode}');
    } on TimeoutException {
      throw Exception('GET $uri timed out');
    } on FormatException catch (error) {
      throw Exception('GET $uri returned invalid JSON: ${error.message}');
    } on http.ClientException catch (error) {
      if (kIsWeb) {
        throw Exception(
          'GET $uri failed in Chrome: ${error.message}. '
          'Open the URL in Chrome to confirm the backend is running. '
          'If it opens there, enable CORS on the backend for the Flutter web origin.',
        );
      }

      throw Exception('GET $uri failed: ${error.message}');
    }
  }

  Future<Item> createEvent({required String title}) async {
    final uri = Uri.parse(_catalogUrl);

    try {
      final response = await http
          .post(
            uri,
            headers: _authHeaders,
            body: jsonEncode({'title': title}),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode == 401) {
        throw Exception('You must be logged in to create an event.');
      }

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('POST $uri failed with status ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic>) {
        throw Exception('POST $uri returned an unexpected response');
      }

      return Item.fromJson(decoded);
    } on TimeoutException {
      throw Exception('POST $uri timed out');
    } on FormatException catch (error) {
      throw Exception('POST $uri returned invalid JSON: ${error.message}');
    } on http.ClientException catch (error) {
      if (kIsWeb) {
        throw Exception(
          'POST $uri failed in Chrome: ${error.message}. '
          'If the endpoint works directly, enable CORS on the backend for the Flutter web origin.',
        );
      }

      throw Exception('POST $uri failed: ${error.message}');
    }
  }

  Future<void> createAccountStake({
    required String id,
    required int stake,
    required String outcome,
  }) async {
    final uri = Uri.parse(_accountUrl);

    try {
      final response = await http
          .post(
            uri,
            headers: _authHeaders,
            body: jsonEncode({
              'eventId': id,
              'stake': stake,
              'outcome': outcome == 'Yes' ? 1 : 2,
            }),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('POST $uri failed with status ${response.statusCode}');
      }
    } on TimeoutException {
      throw Exception('POST $uri timed out');
    } on http.ClientException catch (error) {
      if (kIsWeb) {
        throw Exception(
          'POST $uri failed in Chrome: ${error.message}. '
          'If the endpoint works directly, enable CORS on the backend for the Flutter web origin.',
        );
      }

      throw Exception('POST $uri failed: ${error.message}');
    }
  }

  Future<void> depositToAccount({
    required double amount,
  }) async {
    final uri = Uri.parse(_depositUrl);

    try {
      final response = await http
          .post(
            uri,
            headers: _authHeaders,
            body: jsonEncode({
              'amount': amount,
            }),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('POST $uri failed with status ${response.statusCode}');
      }
    } on TimeoutException {
      throw Exception('POST $uri timed out');
    } on http.ClientException catch (error) {
      if (kIsWeb) {
        throw Exception(
          'POST $uri failed in Chrome: ${error.message}. '
          'If the endpoint works directly, enable CORS on the backend for the Flutter web origin.',
        );
      }

      throw Exception('POST $uri failed: ${error.message}');
    }
  }

  Future<double> fetchBalance() async {
    final uri = Uri.parse('http://$_host:$_gatewayPort/account/api/v1/account/balance');

    try {
      final response = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 10));

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('GET $uri failed with status ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) {
        final amountValue = decoded['amount'] ?? decoded['balance'] ?? decoded['Amount'];
        if (amountValue is num) {
          return amountValue.toDouble();
        }
        if (amountValue is String) {
          final parsed = double.tryParse(amountValue);
          if (parsed != null) {
            return parsed;
          }
        }
      }

      if (decoded is num) {
        return decoded.toDouble();
      }
      if (decoded is String) {
        final parsed = double.tryParse(decoded);
        if (parsed != null) {
          return parsed;
        }
      }

      throw Exception('GET $uri returned an unexpected response');
    } on TimeoutException {
      throw Exception('GET $uri timed out');
    } on FormatException catch (error) {
      throw Exception('GET $uri returned invalid JSON: ${error.message}');
    } on http.ClientException catch (error) {
      if (kIsWeb) {
        throw Exception(
          'GET $uri failed in Chrome: ${error.message}. '
          'If the endpoint works directly, enable CORS on the backend for the Flutter web origin.',
        );
      }

      throw Exception('GET $uri failed: ${error.message}');
    }
  }

  Future<void> register({
    required String name,
    required String email,
    required String password,
    required bool isAdmin,
  }) async {
    final uri = Uri.parse('$_identityUrl/register');

    try {
      final response = await http
          .post(
            uri,
            headers: const {'Content-Type': 'application/json'},
            body: jsonEncode({
              'name': name,
              'email': email,
              'password': password,
              'isAdmin': isAdmin,
            }),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode == 409) {
        throw Exception('Email is already registered.');
      }

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('POST $uri failed with status ${response.statusCode}');
      }
    } on TimeoutException {
      throw Exception('POST $uri timed out');
    } on http.ClientException catch (error) {
      if (kIsWeb) {
        throw Exception(
          'POST $uri failed in Chrome: ${error.message}. '
          'If the endpoint works directly, enable CORS on the backend for the Flutter web origin.',
        );
      }

      throw Exception('POST $uri failed: ${error.message}');
    }
  }

  Future<String> login({
    required String email,
    required String password,
  }) async {
    final uri = Uri.parse('$_identityUrl/login');

    try {
      final response = await http
          .post(
            uri,
            headers: const {'Content-Type': 'application/json'},
            body: jsonEncode({'email': email, 'password': password}),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode == 401) {
        throw Exception('Invalid email or password.');
      }

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('POST $uri failed with status ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic> || decoded['token'] is! String) {
        throw Exception('POST $uri returned an unexpected response');
      }

      return decoded['token'] as String;
    } on TimeoutException {
      throw Exception('POST $uri timed out');
    } on FormatException catch (error) {
      throw Exception('POST $uri returned invalid JSON: ${error.message}');
    } on http.ClientException catch (error) {
      if (kIsWeb) {
        throw Exception(
          'POST $uri failed in Chrome: ${error.message}. '
          'If the endpoint works directly, enable CORS on the backend for the Flutter web origin.',
        );
      }

      throw Exception('POST $uri failed: ${error.message}');
    }
  }
}
