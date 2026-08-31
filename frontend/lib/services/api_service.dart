import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import '../models/item.dart';
import '../models/price_history_point.dart';
import 'api_routes.dart';
import 'auth_service.dart';

class ApiService {
  Map<String, String> get _authHeaders {
    final token = AuthService.instance.token;
    return {
      'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };
  }

  Future<List<Item>> fetchItems() async {
    final uri = ApiRoutes.events;

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

  Future<List<PriceHistoryPoint>> fetchPriceHistory(String eventId) async {
  final uri = ApiRoutes.eventHistory(eventId);

  try {
    final response = await http.get(uri).timeout(const Duration(seconds: 10));

    if (response.statusCode != 200) {
      throw Exception('GET $uri failed with status ${response.statusCode}');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw Exception('GET $uri returned an unexpected response');
    }

    return decoded
        .map((json) => PriceHistoryPoint.fromJson(json as Map<String, dynamic>))
        .toList();
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

  Future<Item> fetchItem(String id) async {
    final uri = ApiRoutes.event(id);

    try {
      final response = await http.get(uri).timeout(const Duration(seconds: 10));

      if (response.statusCode != 200) {
        throw Exception('GET $uri failed with status ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic>) {
        throw Exception('GET $uri returned an unexpected response');
      }

      return Item.fromJson(decoded);
    } on TimeoutException {
      throw Exception('GET $uri timed out');
    } on FormatException catch (error) {
      throw Exception('GET $uri returned invalid JSON: ${error.message}');
    } on http.ClientException catch (error) {
      throw Exception('GET $uri failed: ${error.message}');
    }
  }

  Future<Map<String, String>> fetchEventNames(List<String> ids) async {
    if (ids.isEmpty) return {};

    final uri = Uri.parse('$_catalogUrl/by-ids');

    try {
      final response = await http
          .post(
            uri,
            headers: _authHeaders,
            body: jsonEncode({'ids': ids}),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode != 200) {
        throw Exception('POST $uri failed with status ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! List) {
        throw Exception('POST $uri returned an unexpected response');
      }

      final names = <String, String>{};
      for (final entry in decoded) {
        if (entry is! Map<String, dynamic>) continue;
        final id = (entry['id'] ?? entry['Id'] ?? '').toString();
        final title = (entry['title'] ?? entry['Title'] ?? '').toString();
        if (id.isEmpty) continue;
        names[id] = title.isEmpty ? id : title;
      }
      return names;
    } on TimeoutException {
      throw Exception('POST $uri timed out');
    } on FormatException catch (error) {
      throw Exception('POST $uri returned invalid JSON: ${error.message}');
    } on http.ClientException catch (error) {
      throw Exception('POST $uri failed: ${error.message}');
    }
  }

  Future<Item> createEvent({required String title}) async {
    final uri = ApiRoutes.events;

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

  Future<void> resolveEvent({
    required String id,
    required int outcome,
  }) async {
    final uri = ApiRoutes.resolveEvent;

    try {
      final response = await http
          .post(
            uri,
            headers: _authHeaders,
            body: jsonEncode({
              'id': id,
              'outcome': outcome,
            }),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('POST $uri failed with status ${response.statusCode}');
      }
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
    required double expectedPrice,
    double? slippageDelta,
    double? spotPriceWindow,
  }) async {
    final uri = ApiRoutes.buy;

    try {
      final response = await http
          .post(
            uri,
            headers: _authHeaders,
            body: jsonEncode({
              'eventId': id,
              'stake': stake,
              'outcome': outcome == 'Yes' ? 1 : 2,
              'expectedPrice': expectedPrice,
              'spotPriceWindow': spotPriceWindow,
              if (slippageDelta != null) 'slippageDelta': slippageDelta,
            }),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode == 401) {
        throw Exception('Please log in to place a bet.');
      }

      if (response.statusCode == 409) {
        throw Exception(
          response.body.isNotEmpty
              ? response.body
              : 'This bet could not be placed right now.',
        );
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

  Future<void> depositToAccount({
    required double amount,
  }) async {
    final uri = ApiRoutes.deposit;

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

  Future<void> sellShares({
    required String eventId,
    required double shares,
    required String outcome,
    required double expectedPrice,
    double? slippageDelta,
  }) async {
    final uri = ApiRoutes.sellShares;

    try {
      final response = await http
          .post(
            uri,
            headers: _authHeaders,
            body: jsonEncode({
              'eventId': eventId,
              'shares': shares,
              'outcome': outcome == 'Yes' ? 1 : 2,
              'expectedPrice': expectedPrice,
              if (slippageDelta != null) 'slippageDelta': slippageDelta,
            }),
          )
          .timeout(const Duration(seconds: 10));

      final message = response.body.isNotEmpty ? response.body : 'Unknown error';

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('POST $uri failed with status ${response.statusCode}: $message');
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
    final uri = ApiRoutes.balance;

    try {
      final response = await http
          .get(uri, headers: _authHeaders)
          .timeout(const Duration(seconds: 10));

      if (response.statusCode == 401) {
        throw Exception('Please log in to view your balance.');
      }

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('GET $uri failed with status ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) {
        final amountValue =
            decoded['amount'] ?? decoded['balance'] ?? decoded['Amount'];
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

  Future<List<Map<String, dynamic>>> fetchTransactions() async {
    final uri = ApiRoutes.transactions;

    try {
      final response = await http
          .get(uri, headers: _authHeaders)
          .timeout(const Duration(seconds: 10));

      if (response.statusCode == 401) {
        throw Exception('Please log in to view your transactions.');
      }

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('GET $uri failed with status ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! List) {
        throw Exception('GET $uri returned an unexpected response');
      }

      return decoded
          .map((item) => item as Map<String, dynamic>)
          .toList(growable: false);
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
    final uri = ApiRoutes.register;

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
    final uri = ApiRoutes.login;

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
