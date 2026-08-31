import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_grid_app/main.dart';
import 'package:flutter_grid_app/screens/balance_screen.dart';

void main() {
  testWidgets('App smoke test', (WidgetTester tester) async {
    await tester.pumpWidget(const MyApp());
    expect(find.byType(MaterialApp), findsOneWidget);
  });

  testWidgets('Profile screen shows account info and keeps deposit action',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: BalanceScreen(),
      ),
    );

    expect(find.text('Profile'), findsOneWidget);
    expect(find.text('Account'), findsOneWidget);
    expect(find.text('Deposit now'), findsOneWidget);
    expect(find.byType(TextFormField), findsOneWidget);
    expect(find.text('Your shares'), findsOneWidget);
  });
}
