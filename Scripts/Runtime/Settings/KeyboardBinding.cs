using System;
using System.Collections.Generic;

namespace PranaTactics.Settings
{
    /// <summary>
    /// キーボードの保存名と左右共通の修飾キーを定義します。
    /// </summary>
    public static class KeyboardBinding
    {
        private static readonly string[] s_values = BuildValues();

        public static IReadOnlyList<string> Values => s_values;

        public static string Normalize(string value)
        {
            switch (value)
            {
                case "LeftShift":
                case "RightShift":
                    return "Shift";
                case "LeftControl":
                case "RightControl":
                    return "Ctrl";
                case "LeftAlt":
                case "RightAlt":
                case "AltGr":
                    return "Alt";
                case "LeftWindows":
                case "RightWindows":
                    return "Windows";
                case "LeftCommand":
                case "RightCommand":
                case "LeftApple":
                case "RightApple":
                    return "Command";
                default:
                    return value;
            }
        }

        public static string Validate(string value, string fallback)
        {
            string normalized = Normalize(value);

            return Array.IndexOf(s_values, normalized) >= 0 ? normalized : fallback;
        }

        public static string Display(string value)
        {
            if (value.StartsWith("Alpha", StringComparison.Ordinal))
            {
                return value.Substring(5);
            }

            if (value.StartsWith("Keypad", StringComparison.Ordinal))
            {
                return "テンキー " + value.Substring(6);
            }

            return value == "Return" ? "Enter" : value;
        }

        private static string[] BuildValues()
        {
            var values = new List<string>(new[] {
                "Shift", "Ctrl", "Alt", "Windows", "Command",
                "Backspace", "Tab", "Clear", "Return", "Pause", "Escape", "Space", "Delete",
                "Exclaim", "DoubleQuote", "Hash", "Dollar", "Percent", "Ampersand", "Quote",
                "LeftParen", "RightParen", "Asterisk", "Plus", "Comma", "Minus", "Period", "Slash",
                "Colon", "Semicolon", "Less", "Equals", "Greater", "Question", "At",
                "LeftBracket", "Backslash", "RightBracket", "Caret", "Underscore", "BackQuote",
                "LeftCurlyBracket", "Pipe", "RightCurlyBracket", "Tilde",
                "KeypadPeriod", "KeypadDivide", "KeypadMultiply", "KeypadMinus", "KeypadPlus", "KeypadEnter", "KeypadEquals",
                "UpArrow", "DownArrow", "RightArrow", "LeftArrow", "Insert", "Home", "End", "PageUp", "PageDown",
                "Numlock", "CapsLock", "ScrollLock", "Help", "Print", "SysReq", "Break", "Menu"
            });

            for (char key = 'A'; key <= 'Z'; key++)
            {
                values.Add(key.ToString());
            }

            for (int key = 0; key <= 9; key++)
            {
                values.Add("Alpha" + key);
                values.Add("Keypad" + key);
            }

            for (int key = 1; key <= 15; key++)
            {
                values.Add("F" + key);
            }

            return values.ToArray();
        }
    }
}
