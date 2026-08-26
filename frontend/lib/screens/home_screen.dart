import 'package:flutter/material.dart';
import '../models/item.dart';
import '../services/api_service.dart';
import '../services/auth_service.dart';
import '../widgets/item_card.dart';
import 'create_event_screen.dart';
import 'detail_screen.dart';
import 'login_screen.dart';
import 'balance_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  final ApiService _api = ApiService();
  late Future<List<Item>> _itemsFuture;

  @override
  void initState() {
    super.initState();
    _itemsFuture = _api.fetchItems();
  }

  void _refresh() {
    setState(() {
      _itemsFuture = _api.fetchItems();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: const Color(0xFF070A0F),
        elevation: 0,
        title: Row(
          children: [
            Container(
              width: 28,
              height: 28,
              decoration: BoxDecoration(
                color: const Color(0xFF00A3FF),
                borderRadius: BorderRadius.circular(6),
              ),
              child:
                  const Icon(Icons.trending_up, color: Colors.white, size: 18),
            ),
            const SizedBox(width: 8),
            const Text(
              'Eventus',
              style: TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.bold,
                fontSize: 20,
              ),
            ),
          ],
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.search, color: Colors.white),
            onPressed: () {},
          ),
          IconButton(
            icon: const Icon(
              Icons.person_outline,
              color: Colors.white,
            ),
            tooltip: 'Profile',
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => const BalanceScreen()),
              );
            },
          ),
          ListenableBuilder(
            listenable: AuthService.instance,
            builder: (context, _) {
              final loggedIn = AuthService.instance.isLoggedIn;
              return Row(
                children: [
                  if (loggedIn)
                    IconButton(
                      icon: const Icon(Icons.add_circle_outline,
                          color: Colors.white),
                      tooltip: 'Create event',
                      onPressed: () async {
                        final created = await Navigator.push<Item>(
                          context,
                          MaterialPageRoute(
                            builder: (_) => const CreateEventScreen(),
                          ),
                        );
                        if (created == null) return;
                        _refresh();
                        if (!context.mounted) return;
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => DetailScreen(item: created),
                          ),
                        );
                      },
                    ),
                  IconButton(
                    icon: Icon(
                      loggedIn ? Icons.logout : Icons.login,
                      color: Colors.white,
                    ),
                    tooltip: loggedIn ? 'Logout' : 'Login',
                    onPressed: () {
                      if (loggedIn) {
                        AuthService.instance.logout();
                            final messenger = ScaffoldMessenger.maybeOf(context);
                            messenger?.showSnackBar(
                              const SnackBar(
                                content: Text('Logged out'),
                                backgroundColor: Color(0xFF111827),
                                behavior: SnackBarBehavior.floating,
                              ),
                            );
                      } else {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => const LoginScreen(),
                          ),
                        );
                      }
                    },
                  ),
                ],
              );
            },
          ),
        ],
      ),
      body: FutureBuilder<List<Item>>(
        future: _itemsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(
              child: CircularProgressIndicator(color: Color(0xFF00A3FF)),
            );
          }

          if (snapshot.hasError) {
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.wifi_off, color: Colors.white38, size: 48),
                  const SizedBox(height: 12),
                  const Text(
                    'Failed to load items',
                    style: TextStyle(color: Colors.white54),
                  ),
                  const SizedBox(height: 8),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 24),
                    child: Text(
                      snapshot.error.toString(),
                      textAlign: TextAlign.center,
                      style:
                          const TextStyle(color: Colors.white38, fontSize: 12),
                    ),
                  ),
                  const SizedBox(height: 16),
                  ElevatedButton(
                    onPressed: _refresh,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF00A3FF),
                    ),
                    child: const Text('Retry'),
                  ),
                ],
              ),
            );
          }

          final items = snapshot.data!;

          return RefreshIndicator(
            color: const Color(0xFF00A3FF),
            backgroundColor: const Color(0xFF111827),
            onRefresh: () async => _refresh(),
            child: ListView(
              padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
              children: [
                for (final item in items) ...[
                  ItemCard(
                    item: item,
                    onTap: () async {
                      await Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (_) => DetailScreen(item: item),
                        ),
                      );
                      if (!context.mounted) return;
                      _refresh();
                    },
                  ),
                  const SizedBox(height: 10),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}

