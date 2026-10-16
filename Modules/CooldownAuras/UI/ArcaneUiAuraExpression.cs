using System;
using System.Globalization;
using ArcanesGroupFrames.Modules.CooldownAuras.Editor;

namespace ArcanesGroupFrames.Modules.CooldownAuras.UI
{
    /// <summary>
    /// Phase 11.4C: bounded, read-only expression language. No C# compilation,
    /// reflection from user text, assignments, game-method calls or file access.
    /// All data is obtained through the existing verified resource readers.
    /// </summary>
    internal static class ArcaneUiAuraExpression
    {
        internal const int MaxLength = 240;
        internal const string Variables =
            "player_hp, player_hp_max, player_hp_pct, player_mana, player_mana_max, " +
            "player_mana_pct, target_exists, target_hp, target_hp_max, target_hp_pct, " +
            "target_mana, target_mana_max, target_mana_pct, absorb, heat, flashfire, wildfire, combust";

        private static readonly string[] Keys = {
            "player_hp", "player_hp_max", "player_hp_pct", "player_mana", "player_mana_max", "player_mana_pct",
            "target_exists", "target_hp", "target_hp_max", "target_hp_pct", "target_mana", "target_mana_max",
            "target_mana_pct", "absorb", "heat", "flashfire", "wildfire", "combust"
        };

        internal static bool TryRead(CustomAuraDefinition aura, out float value, out float maximum, out bool visible,
            out string error)
        {
            value = maximum = 0f; visible = false; error = "";
            if (aura == null) { error = "No aura selected"; return false; }
            if (!TryEvaluate(aura.ExpressionValue, ResolveLive, out value, out error)) return false;
            if (!string.IsNullOrWhiteSpace(aura.ExpressionMaximum) &&
                !TryEvaluate(aura.ExpressionMaximum, ResolveLive, out maximum, out error)) return false;
            if (maximum < 0f) { error = "Maximum cannot be negative"; return false; }
            if (string.IsNullOrWhiteSpace(aura.ExpressionCondition))
                visible = value >= aura.MinimumValue && (aura.ShowWhenEmpty || value > 0f);
            else
            {
                float result;
                if (!TryEvaluate(aura.ExpressionCondition, ResolveLive, out result, out error)) return false;
                visible = result != 0f && value >= aura.MinimumValue;
            }
            return true;
        }

        internal static bool Validate(CustomAuraDefinition aura, out string message)
        {
            message = "";
            if (aura == null) { message = "No aura selected"; return false; }
            float result;
            // Syntax-only validation uses well-defined sample data, never a
            // fictional live resource value. A separate LIVE TEST resolves real data.
            Func<string, float?> sample = name => IsAllowed(name) ? (float?)1f : null;
            string error;
            if (!TrySyntax(aura.ExpressionValue, sample, out result, out error))
            { message = "Value: " + error; return false; }
            if (!string.IsNullOrWhiteSpace(aura.ExpressionMaximum) &&
                !TrySyntax(aura.ExpressionMaximum, sample, out result, out error))
            { message = "Maximum: " + error; return false; }
            if (!string.IsNullOrWhiteSpace(aura.ExpressionCondition) &&
                !TrySyntax(aura.ExpressionCondition, sample, out result, out error))
            { message = "Condition: " + error; return false; }
            message = "Valid expression syntax (this does not guarantee live data availability).";
            return true;
        }

        private static bool IsAllowed(string name)
        {
            for (int i = 0; i < Keys.Length; i++)
                if (string.Equals(Keys[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static float? ResolveLive(string symbol)
        {
            symbol = symbol.ToLowerInvariant();
            if (symbol == "target_exists")
                return GameData.PlayerControl != null && GameData.PlayerControl.CurrentTarget != null &&
                    GameData.PlayerControl.CurrentTarget.MyStats != null ? 1f : 0f;
            string trigger = null;
            switch (symbol)
            {
                case "player_hp": case "player_hp_max": case "player_hp_pct": trigger = "Player Health"; break;
                case "player_mana": case "player_mana_max": case "player_mana_pct": trigger = "Player Mana"; break;
                case "target_hp": case "target_hp_max": case "target_hp_pct": trigger = "Target Health"; break;
                case "target_mana": case "target_mana_max": case "target_mana_pct": trigger = "Target Mana"; break;
                case "absorb": trigger = "Player Absorb"; break;
                case "heat": trigger = "Emberweaver Heat"; break;
                case "flashfire": trigger = "Emberweaver Flashfire"; break;
                case "wildfire": trigger = "Emberweaver Wildfire Stored"; break;
                case "combust": trigger = "Emberweaver Combust Projected"; break;
            }
            if (trigger == null) return null;
            float current, maximum;
            if (!ArcaneUiAuraResourceReader.TryRead(trigger, out current, out maximum)) return null;
            if (symbol.EndsWith("_max", StringComparison.Ordinal)) return maximum;
            if (symbol.EndsWith("_pct", StringComparison.Ordinal))
                return maximum > 0f ? current / maximum * 100f : (float?)null;
            return current;
        }

        internal static bool TryEvaluate(string expression, Func<string, float?> resolver, out float value,
            out string error)
        {
            value = 0f; error = "";
            if (string.IsNullOrWhiteSpace(expression)) { error = "Expression is empty"; return false; }
            if (expression.Length > MaxLength) { error = "Expression exceeds 240 characters"; return false; }
            if (resolver == null) { error = "No read-only resolver"; return false; }
            try
            {
                var parser = new Parser(expression, resolver);
                value = parser.Run();
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new FormatException("Result is not finite");
                return true;
            }
            catch (Exception exception)
            {
                // Only language errors appear in the editor, not a call stack
                // or an exception that can interrupt native gameplay Update.
                error = exception is FormatException ? exception.Message :
                    "Expression evaluation unavailable (" + exception.GetType().Name + ")";
                value = 0f;
                return false;
            }
        }

        private static bool TrySyntax(string expression, Func<string, float?> resolver,
            out float value, out string error)
        {
            value = 0f; error = "";
            if (string.IsNullOrWhiteSpace(expression)) { error = "Expression is empty"; return false; }
            if (expression.Length > MaxLength) { error = "Expression exceeds 240 characters"; return false; }
            try { value = new Parser(expression, resolver, true).Run(); return true; }
            catch (FormatException ex) { error = ex.Message; return false; }
            catch (Exception ex) { error = "Invalid expression (" + ex.GetType().Name + ")"; return false; }
        }

        private sealed class Parser
        {
            private readonly string _source;
            private readonly Func<string, float?> _resolver;
            private int _index, _steps, _depth;
            private readonly bool _syntaxOnly;
            internal Parser(string source, Func<string, float?> resolver, bool syntaxOnly = false)
            { _source = source; _resolver = resolver; _syntaxOnly = syntaxOnly; }

            internal float Run()
            {
                float value = Or();
                Space();
                if (_index != _source.Length) Fail("Unexpected token");
                return Finite(value);
            }
            private static bool Truth(float x) { return x != 0f; }
            private void Tick()
            {
                if (++_steps > 256) Fail("Expression operation limit exceeded");
            }
            private float Finite(float x)
            {
                Tick();
                if (float.IsNaN(x) || float.IsInfinity(x)) Fail("Invalid numeric result");
                return x;
            }
            private void Space() { while (_index < _source.Length && char.IsWhiteSpace(_source[_index])) _index++; }
            private bool Take(string op)
            {
                Space();
                if (_index + op.Length > _source.Length ||
                    string.CompareOrdinal(_source, _index, op, 0, op.Length) != 0) return false;
                _index += op.Length;
                Tick(); return true;
            }
            private void Fail(string reason)
            { throw new FormatException(reason + " near column " + (_index + 1)); }
            private float Or()
            {
                float a = And();
                while (Take("||")) { float b = And(); a = Truth(a) || Truth(b) ? 1f : 0f; }
                return a;
            }
            private float And()
            {
                float a = Compare();
                while (Take("&&")) { float b = Compare(); a = Truth(a) && Truth(b) ? 1f : 0f; }
                return a;
            }
            private float Compare()
            {
                float a = Add();
                while (true)
                {
                    if (Take(">=")) { float b = Add(); a = a >= b ? 1f : 0f; }
                    else if (Take("<=")) { float b = Add(); a = a <= b ? 1f : 0f; }
                    else if (Take("==")) { float b = Add(); a = a == b ? 1f : 0f; }
                    else if (Take("!=")) { float b = Add(); a = a != b ? 1f : 0f; }
                    else if (Take(">")) { float b = Add(); a = a > b ? 1f : 0f; }
                    else if (Take("<")) { float b = Add(); a = a < b ? 1f : 0f; }
                    else break;
                }
                return a;
            }
            private float Add()
            {
                float a = Multiply();
                while (true)
                {
                    if (Take("+")) a = Finite(a + Multiply());
                    else if (Take("-")) a = Finite(a - Multiply());
                    else break;
                }
                return a;
            }
            private float Multiply()
            {
                float a = Unary();
                while (true)
                {
                    if (Take("*")) a = Finite(a * Unary());
                    else if (Take("/"))
                    {
                        float b = Unary();
                        if (Math.Abs(b) < .0000001f)
                        { if (_syntaxOnly) a = 0f; else Fail("Division by zero"); }
                        else a = Finite(a / b);
                    }
                    else if (Take("%"))
                    {
                        float b = Unary();
                        if (Math.Abs(b) < .0000001f)
                        { if (_syntaxOnly) a = 0f; else Fail("Division by zero"); }
                        else a = Finite(a % b);
                    }
                    else break;
                }
                return a;
            }
            private float Unary()
            {
                if (Take("!")) return Truth(Unary()) ? 0f : 1f;
                if (Take("-")) return Finite(-Unary());
                if (Take("+")) return Unary();
                return Atom();
            }
            private float Atom()
            {
                Space();
                if (Take("("))
                {
                    if (++_depth > 20) Fail("Too many nested brackets");
                    float v = Or();
                    if (!Take(")")) Fail("Missing closing bracket");
                    _depth--; return v;
                }
                if (_index >= _source.Length) Fail("Expected number or variable");
                char c = _source[_index];
                if (char.IsDigit(c) || c == '.')
                {
                    int begin = _index;
                    while (_index < _source.Length && (char.IsDigit(_source[_index]) || _source[_index] == '.')) _index++;
                    string token = _source.Substring(begin, _index - begin);
                    float number;
                    if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                        Fail("Invalid number");
                    return Finite(number);
                }
                if (!char.IsLetter(c) && c != '_') Fail("Unexpected character");
                int start = _index++;
                while (_index < _source.Length &&
                    (char.IsLetterOrDigit(_source[_index]) || _source[_index] == '_')) _index++;
                string name = _source.Substring(start, _index - start).ToLowerInvariant();
                if (Take("("))
                {
                    if (++_depth > 20) Fail("Function nesting too deep");
                    // Only exact named functions; no user-defined or native calls.
                    float a = Or();
                    float b = 0f, d = 0f;
                    int argc = 1;
                    if (Take(",")) { b = Or(); argc++; }
                    if (Take(",")) { d = Or(); argc++; }
                    if (!Take(")")) Fail("Function requires closing bracket");
                    _depth--;
                    if (name == "abs" && argc == 1) return Finite(Math.Abs(a));
                    if (name == "floor" && argc == 1) return Finite((float)Math.Floor(a));
                    if (name == "ceil" && argc == 1) return Finite((float)Math.Ceiling(a));
                    if (name == "round" && argc == 1) return Finite((float)Math.Round(a));
                    if (name == "min" && argc == 2) return Finite(Math.Min(a,b));
                    if (name == "max" && argc == 2) return Finite(Math.Max(a,b));
                    if (name == "clamp" && argc == 3) return Finite(Math.Min(Math.Max(a,b),d));
                    Fail("Unknown function or incorrect argument count: " + name);
                }
                if (!IsAllowed(name)) Fail("Unknown symbol: " + name);
                float? value = _resolver(name);
                if (!value.HasValue) Fail("Live data unavailable: " + name);
                return Finite(value.Value);
            }
        }
    }
}
