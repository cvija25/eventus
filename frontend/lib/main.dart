import 'package:flutter/material.dart';
import 'screens/home_screen.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Eventus',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF00A3FF),
          brightness: Brightness.dark,
        ),
        scaffoldBackgroundColor: const Color(0xFF070A0F),
        useMaterial3: true,
      ),
      home: const HomeScreen(),
    );
  }
}
