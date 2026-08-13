import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../models/item.dart';
import '../services/api_service.dart';

class DetailScreen extends StatefulWidget {
  final Item item;

  const DetailScreen({super.key, required this.item});

  @override
  State<DetailScreen> createState() => _DetailScreenState();
}

class _DetailScreenState extends State<DetailScreen> {
  final ApiService _api = ApiService();
  final TextEditingController _controller = TextEditingController();
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  bool _submitted = false;
  bool _submitting = false;
  int? _submittedValue;
  String? _submitError;
  String? _selectedOutcome;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_selectedOutcome == null) {
      setState(() {
        _submitError = 'Select an outcome first';
        _submitted = false;
      });
      return;
    }

    if (_formKey.currentState?.validate() ?? false) {
      final value = int.parse(_controller.text);
      setState(() {
        _submitting = true;
        _submitted = false;
        _submittedValue = value;
        _submitError = null;
      });
      FocusScope.of(context).unfocus();

      try {
        await _api.createAccountStake(
          id: widget.item.id,
          stake: value,
          outcome: _selectedOutcome!,
        );
        if (!mounted) return;

        setState(() {
          _submitted = true;
          _submitting = false;
        });

        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Stake submitted: $value'),
            backgroundColor: const Color(0xFF111827),
            behavior: SnackBarBehavior.floating,
            duration: const Duration(seconds: 2),
          ),
        );
      } catch (error) {
        if (!mounted) return;

        setState(() {
          _submitting = false;
          _submitError = error.toString();
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final item = widget.item;

    return Scaffold(
      backgroundColor: const Color(0xFF070A0F),
      appBar: AppBar(
        backgroundColor: const Color(0xFF070A0F),
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Colors.white),
          onPressed: () => Navigator.pop(context),
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.share_outlined, color: Colors.white),
            onPressed: () {},
          ),
          IconButton(
            icon: const Icon(Icons.more_vert, color: Colors.white),
            onPressed: () {},
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _Panel(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      _Pill(label: item.category),
                      const SizedBox(width: 8),
                      Text(
                        item.closeLabel,
                        style: const TextStyle(
                            color: Colors.white54, fontSize: 12),
                      ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  Text(
                    item.title,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 22,
                      fontWeight: FontWeight.w800,
                      height: 1.18,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            _OutcomePanel(
              item: item,
              selectedOutcome: _selectedOutcome,
              onOutcomeSelected: (outcome) {
                setState(() {
                  _selectedOutcome = outcome;
                  _submitError = null;
                  _submitted = false;
                });
              },
            ),
            const SizedBox(height: 12),
            _TradePanel(
              formKey: _formKey,
              controller: _controller,
              submitted: _submitted,
              submitting: _submitting,
              submittedValue: _submittedValue,
              submitError: _submitError,
              onSubmit: _submit,
            ),
            const SizedBox(height: 12),
            _InfoPanel(item: item),
          ],
        ),
      ),
    );
  }
}

class _OutcomePanel extends StatelessWidget {
  final Item item;
  final String? selectedOutcome;
  final ValueChanged<String> onOutcomeSelected;

  const _OutcomePanel({
    required this.item,
    required this.selectedOutcome,
    required this.onOutcomeSelected,
  });

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Outcome prices',
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 14),
          ClipRRect(
            borderRadius: BorderRadius.circular(999),
            child: LinearProgressIndicator(
              value: item.priceYes / 100,
              minHeight: 10,
              backgroundColor: const Color(0xFF2B1118),
              valueColor:
                  const AlwaysStoppedAnimation<Color>(Color(0xFF00A3FF)),
            ),
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(
                child: _OutcomeButton(
                  label: 'Yes',
                  price: item.priceYes,
                  color: const Color(0xFF00A3FF),
                  selected: selectedOutcome == 'Yes',
                  onTap: () => onOutcomeSelected('Yes'),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _OutcomeButton(
                  label: 'No',
                  price: item.priceNo,
                  color: const Color(0xFFEF4444),
                  selected: selectedOutcome == 'No',
                  onTap: () => onOutcomeSelected('No'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _TradePanel extends StatelessWidget {
  final GlobalKey<FormState> formKey;
  final TextEditingController controller;
  final bool submitted;
  final bool submitting;
  final int? submittedValue;
  final String? submitError;
  final VoidCallback onSubmit;

  const _TradePanel({
    required this.formKey,
    required this.controller,
    required this.submitted,
    required this.submitting,
    required this.submittedValue,
    required this.submitError,
    required this.onSubmit,
  });

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Place order',
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 12),
          Form(
            key: formKey,
            child: TextFormField(
              controller: controller,
              keyboardType: TextInputType.number,
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp(r'^\d*')),
              ],
              style: const TextStyle(color: Colors.white, fontSize: 16),
              decoration: InputDecoration(
                hintText: 'Shares',
                hintStyle: const TextStyle(color: Colors.white38),
                filled: true,
                fillColor: const Color(0xFF111827),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: BorderSide.none,
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide:
                      const BorderSide(color: Color(0xFF00A3FF), width: 1.5),
                ),
                errorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: Color(0xFFEF4444)),
                ),
                focusedErrorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: Color(0xFFEF4444)),
                ),
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: 14,
                  vertical: 13,
                ),
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) {
                  return 'Enter share amount';
                }
                final amount = int.tryParse(value);
                if (amount == null || amount <= 0) {
                  return 'Use a positive whole number';
                }
                return null;
              },
            ),
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: submitting ? null : onSubmit,
              style: FilledButton.styleFrom(
                backgroundColor: const Color(0xFF00A3FF),
                foregroundColor: Colors.white,
                disabledBackgroundColor: const Color(0xFF263244),
                disabledForegroundColor: Colors.white54,
                padding: const EdgeInsets.symmetric(vertical: 14),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(8),
                ),
              ),
              child: Text(submitting ? 'Submitting...' : 'Preview trade'),
            ),
          ),
          if (submitError != null) ...[
            const SizedBox(height: 12),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFF3A1010),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: const Color(0xFF7F1D1D)),
              ),
              child: Text(
                submitError!,
                style: const TextStyle(
                  color: Color(0xFFFCA5A5),
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ],
          if (submitted && submittedValue != null) ...[
            const SizedBox(height: 12),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFF052E1A),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: const Color(0xFF166534)),
              ),
              child: Text(
                'Stake submitted: $submittedValue',
                style: const TextStyle(
                  color: Color(0xFF86EFAC),
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _InfoPanel extends StatelessWidget {
  final Item item;

  const _InfoPanel({required this.item});

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Market info',
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            'Owner ${_shortGuid(item.ownerId)} created this event market. The pot is split by the final resolved outcome.',
            style: const TextStyle(
                color: Colors.white70, fontSize: 14, height: 1.45),
          ),
          const Divider(color: Colors.white12, height: 24),
          Row(
            children: [
              Expanded(
                  child: _Stat(
                      label: 'Pot size', value: '\$${_money(item.potSize)}')),
              Expanded(
                  child: _Stat(
                      label: 'Liquidity',
                      value: '\$${_money(item.liquidity)}')),
            ],
          ),
        ],
      ),
    );
  }

  String _money(int value) {
    if (value >= 1000000) return '${(value / 1000000).toStringAsFixed(1)}M';
    if (value >= 1000) return '${(value / 1000).toStringAsFixed(0)}K';
    return value.toString();
  }

  String _shortGuid(String value) {
    if (value.length <= 8) return value;
    return value.substring(0, 8);
  }
}

class _Panel extends StatelessWidget {
  final Widget child;

  const _Panel({required this.child});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFF0D1320),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: const Color(0xFF1F2937)),
      ),
      child: child,
    );
  }
}

class _Pill extends StatelessWidget {
  final String label;

  const _Pill({required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(
        color: const Color(0xFF111827),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: const Color(0xFF1F2937)),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: Colors.white70,
          fontSize: 12,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _OutcomeButton extends StatelessWidget {
  final String label;
  final int price;
  final Color color;
  final bool selected;
  final VoidCallback onTap;

  const _OutcomeButton({
    required this.label,
    required this.price,
    required this.color,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
    onTap: onTap,
    child: Container(
        padding: const EdgeInsets.symmetric(vertical: 12),
        decoration: BoxDecoration(
          color: selected ? color.withOpacity(0.28) : color.withOpacity(0.16),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: selected ? color : color.withOpacity(0.5), width: selected ? 2 : 1),
        ),
        child: Column(
          children: [
            Text(
              label,
              style: TextStyle(
                color: color,
                fontSize: 13,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 3),
            Text(
              '$price cents',
              style: const TextStyle(
                color: Colors.white,
                fontSize: 17,
                fontWeight: FontWeight.w800,
              ),
            ),
          ],
        ),
      )
    );
  }
}

class _Stat extends StatelessWidget {
  final String label;
  final String value;

  const _Stat({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label,
            style: const TextStyle(color: Colors.white38, fontSize: 12)),
        const SizedBox(height: 4),
        Text(
          value,
          style: const TextStyle(
            color: Colors.white,
            fontSize: 15,
            fontWeight: FontWeight.w800,
          ),
        ),
      ],
    );
  }
}
