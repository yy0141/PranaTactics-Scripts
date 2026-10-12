using System;
using System.Collections.Generic;

using PranaTactics.Settings;
using UnityEngine;

namespace PranaTactics.Controls
{
    /// <summary>
    /// 保存されたキーをUnity入力へ変換し、左右どちらの修飾キーも受け付けます。
    /// </summary>
    public static class KeyboardBindingInput
    {
        private static readonly KeyCode[] s_keys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        private static readonly Dictionary<string, List<KeyCode>> s_bindings = BuildBindings();

        private static Dictionary<string, List<KeyCode>> BuildBindings()
        {
            var bindings = new Dictionary<string, List<KeyCode>>();

            foreach (KeyCode key in s_keys)
            {
                if ((int)key <= 0 || (int)key >= (int)KeyCode.Mouse0)
                {
                    continue;
                }

                string name = KeyboardBinding.Validate(key.ToString(), null);

                if (name == null)
                {
                    continue;
                }

                if (!bindings.TryGetValue(name, out List<KeyCode> keys))
                {
                    keys = new List<KeyCode>();
                    bindings.Add(name, keys);
                }

                if (!keys.Contains(key))
                {
                    keys.Add(key);
                }
            }

            return bindings;
        }

        public static bool IsHeld(string binding)
        {
            return Read(binding, false);
        }

        public static bool IsPressed(string binding)
        {
            return Read(binding, true);
        }

        private static bool Read(string binding, bool pressed)
        {
            if (binding != null && s_bindings.TryGetValue(binding, out List<KeyCode> keys))
            {
                foreach (KeyCode key in keys)
                {
                    if (pressed ? Input.GetKeyDown(key) : Input.GetKey(key))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool TryGetPressed(out string binding)
        {
            foreach (KeyCode key in s_keys)
            {
                if ((int)key > 0 && (int)key < (int)KeyCode.Mouse0 && Input.GetKeyDown(key))
                {
                    binding = KeyboardBinding.Validate(key.ToString(), null);

                    if (binding != null)
                    {
                        return true;
                    }
                }
            }

            binding = null;

            return false;
        }
    }
}
