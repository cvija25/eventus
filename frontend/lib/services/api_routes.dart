import 'package:flutter/foundation.dart';

/// Single source of truth for every backend URL the app talks to.
///
/// All traffic goes through the API gateway, which exposes one versioned
/// surface (`/api/v1/...`) and fans out to the individual services.
class ApiRoutes {
  ApiRoutes._();

  static String get _host =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android
          ? '10.0.2.2'
          : 'localhost';

  static const int _gatewayPort = 1234;

  static String get _base => 'http://$_host:$_gatewayPort/api/v1';

  // Identity
  static Uri get register => Uri.parse('$_base/identity/register');
  static Uri get login => Uri.parse('$_base/identity/login');

  // Account
  static Uri get balance => Uri.parse('$_base/account/balance');
  static Uri get transactions => Uri.parse('$_base/account/transactions');
  static Uri get deposit => Uri.parse('$_base/account/deposit');
  static Uri get buy => Uri.parse('$_base/account/buy');
  static Uri get sell => Uri.parse('$_base/account/sell');

  // Catalog
  static Uri get events => Uri.parse('$_base/events');
  static Uri get resolveEvent => Uri.parse('$_base/events/resolve');
  static Uri get priceStream => Uri.parse('$_base/events/stream');
  static Uri event(String id) => Uri.parse('$_base/events/$id');
  static Uri eventHistory(String id) => Uri.parse('$_base/events/$id/history');
}
