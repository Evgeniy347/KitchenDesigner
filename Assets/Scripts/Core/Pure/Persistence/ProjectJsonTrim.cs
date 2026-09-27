using System.Text;

namespace KitchenDesigner.Core
{
    public static class ProjectJsonTrim
    {
        private const string ElementsKey = "elements";
        private const string BasePlateKey = "basePlate";
        private const string SettingsKey = "settings";
        private const string KeyBindingsKey = "keyBindings";
        private const string UndoHistoryKey = "undoHistory";
        private const string RedoHistoryKey = "redoHistory";
        private const string ConvertStateKey = "convertState";
        private const string ChildrenKey = "children";
        private const string CreatedAtUtcKey = "createdAtUtc";
        private const string LevelsKey = "levels";
        private const string EmptyStringText = "\"\"";
        private const string EmptyArrayText = "[]";

        private static readonly TrimmedMember[] RootMembers =
        {
            new TrimmedMember(ElementsKey, MemberAction.ElementArray),
            new TrimmedMember(BasePlateKey, MemberAction.ElementObject),
            new TrimmedMember(SettingsKey, MemberAction.SettingsObject),
            new TrimmedMember(UndoHistoryKey, MemberAction.RecordArray),
            new TrimmedMember(RedoHistoryKey, MemberAction.RecordArray),
            new TrimmedMember(CreatedAtUtcKey, MemberAction.RemoveWhenEmptyString),
            new TrimmedMember(LevelsKey, MemberAction.RemoveWhenEmptyArray),
        };

        private static readonly TrimmedMember[] SettingsMembers =
        {
            new TrimmedMember(KeyBindingsKey, MemberAction.RemoveWhenEmptyArray),
        };

        private static readonly TrimmedMember[] CommandRecordMembers =
        {
            new TrimmedMember(ConvertStateKey, MemberAction.ElementArray),
            new TrimmedMember(ChildrenKey, MemberAction.RecordArray),
        };

        private readonly struct TrimmedMember
        {
            public readonly string Key;
            public readonly MemberAction Action;

            public TrimmedMember(string key, MemberAction action)
            {
                Key = key;
                Action = action;
            }
        }

        public static string Apply(string projectJson)
        {
            if (string.IsNullOrEmpty(projectJson)) return projectJson;
            return new Pass(projectJson).Run() ?? projectJson;
        }

        private enum ObjectKind
        {
            Root,
            Settings,
            CommandRecord,
        }

        private enum MemberAction
        {
            Copy,
            ElementArray,
            ElementObject,
            RecordArray,
            SettingsObject,
            RemoveWhenEmptyString,
            RemoveWhenEmptyArray,
        }

        private sealed class Pass
        {
            private readonly string _s;
            private readonly StringBuilder _out;
            private readonly JsonObjectEdit _element = new JsonObjectEdit();

            public Pass(string source)
            {
                _s = source;
                _out = new StringBuilder(source.Length);
            }

            public string? Run()
            {
                int start = JsonText.SkipWhitespace(_s, 0);
                if (start >= _s.Length || _s[start] != '{') return null;
                _out.Append(_s, 0, start);
                int end = WriteObject(start, ObjectKind.Root);
                if (end < 0) return null;
                _out.Append(_s, end, _s.Length - end);
                return _out.ToString();
            }

            private int WriteObject(int start, ObjectKind kind)
            {
                _out.Append('{');
                int cursor = start + 1;
                int previousValueEnd = cursor;
                bool keptAny = false;
                var handled = new bool[MembersOf(kind).Length];

                int i = JsonText.SkipWhitespace(_s, start + 1);
                while (i < _s.Length && _s[i] != '}')
                {
                    if (_s[i] != '"') return -1;
                    int keyEnd = JsonText.EndOfValue(_s, i);
                    if (keyEnd < 0) return -1;
                    var action = Classify(kind, i, keyEnd, out int slot);

                    i = JsonText.SkipWhitespace(_s, keyEnd);
                    if (i >= _s.Length || _s[i] != ':') return -1;
                    int valueStart = JsonText.SkipWhitespace(_s, i + 1);
                    if (valueStart >= _s.Length) return -1;

                    if (action != MemberAction.Copy)
                    {
                        bool firstOccurrence = !handled[slot];
                        handled[slot] = true;
                        if (!firstOccurrence || !FitsShape(action, _s[valueStart])) action = MemberAction.Copy;
                    }

                    int valueEnd;
                    if (IsDescent(action))
                    {
                        _out.Append(_s, cursor, valueStart - cursor);
                        valueEnd = Descend(action, valueStart);
                        if (valueEnd < 0) return -1;
                        cursor = valueEnd;
                        keptAny = true;
                    }
                    else
                    {
                        valueEnd = JsonText.EndOfValue(_s, valueStart);
                        if (valueEnd < 0) return -1;
                        if (!IsRemovedValue(action, valueStart, valueEnd))
                        {
                            keptAny = true;
                        }
                        else if (keptAny)
                        {
                            _out.Append(_s, cursor, previousValueEnd - cursor);
                            cursor = valueEnd;
                        }
                        else
                        {
                            int afterValue = JsonText.SkipWhitespace(_s, valueEnd);
                            cursor = afterValue < _s.Length && _s[afterValue] == ',' ? afterValue + 1 : valueEnd;
                        }
                    }
                    previousValueEnd = valueEnd;

                    i = JsonText.SkipWhitespace(_s, valueEnd);
                    if (i < _s.Length && _s[i] == ',') i = JsonText.SkipWhitespace(_s, i + 1);
                }
                if (i >= _s.Length) return -1;
                int end = i + 1;
                _out.Append(_s, cursor, end - cursor);
                return end;
            }

            private int WriteArray(int start, bool itemsAreElements)
            {
                _out.Append('[');
                int cursor = start + 1;
                int i = JsonText.SkipWhitespace(_s, start + 1);
                while (i < _s.Length && _s[i] != ']')
                {
                    int itemEnd;
                    if (_s[i] == '{')
                    {
                        _out.Append(_s, cursor, i - cursor);
                        itemEnd = itemsAreElements ? WriteElement(i) : WriteObject(i, ObjectKind.CommandRecord);
                        if (itemEnd < 0) return -1;
                        cursor = itemEnd;
                    }
                    else
                    {
                        itemEnd = JsonText.EndOfValue(_s, i);
                        if (itemEnd < 0) return -1;
                    }
                    i = JsonText.SkipWhitespace(_s, itemEnd);
                    if (i < _s.Length && _s[i] == ',') i = JsonText.SkipWhitespace(_s, i + 1);
                }
                if (i >= _s.Length) return -1;
                int end = i + 1;
                _out.Append(_s, cursor, end - cursor);
                return end;
            }

            private int WriteElement(int start)
            {
                if (!_element.Read(_s, start)) return -1;
                ElementFamilyJsonTrim.MarkRemovals(_element);
                if (_element.AnyRemoved) _element.WriteTo(_out);
                else _out.Append(_s, start, _element.End - start);
                return _element.End;
            }

            private int Descend(MemberAction action, int valueStart) => action switch
            {
                MemberAction.ElementArray => WriteArray(valueStart, true),
                MemberAction.RecordArray => WriteArray(valueStart, false),
                MemberAction.ElementObject => WriteElement(valueStart),
                _ => WriteObject(valueStart, ObjectKind.Settings),
            };

            private static bool IsDescent(MemberAction action) =>
                action == MemberAction.ElementArray || action == MemberAction.RecordArray
                || action == MemberAction.ElementObject || action == MemberAction.SettingsObject;

            private static bool FitsShape(MemberAction action, char first) => action switch
            {
                MemberAction.ElementArray => first == '[',
                MemberAction.RecordArray => first == '[',
                MemberAction.ElementObject => first == '{',
                MemberAction.SettingsObject => first == '{',
                _ => true,
            };

            private bool IsRemovedValue(MemberAction action, int valueStart, int valueEnd) => action switch
            {
                MemberAction.RemoveWhenEmptyString => ValueIs(valueStart, valueEnd, EmptyStringText),
                MemberAction.RemoveWhenEmptyArray => ValueIs(valueStart, valueEnd, EmptyArrayText),
                _ => false,
            };

            private bool ValueIs(int start, int end, string text) =>
                end - start == text.Length && string.CompareOrdinal(_s, start, text, 0, text.Length) == 0;

            private MemberAction Classify(ObjectKind kind, int keyStart, int keyEnd, out int slot)
            {
                var members = MembersOf(kind);
                for (slot = 0; slot < members.Length; slot++)
                    if (KeyIs(keyStart, keyEnd, members[slot].Key)) return members[slot].Action;
                slot = -1;
                return MemberAction.Copy;
            }

            private static TrimmedMember[] MembersOf(ObjectKind kind) => kind switch
            {
                ObjectKind.Root => RootMembers,
                ObjectKind.Settings => SettingsMembers,
                _ => CommandRecordMembers,
            };

            private bool KeyIs(int keyStart, int keyEnd, string key)
            {
                int length = keyEnd - keyStart - 2;
                if (_s.IndexOf('\\', keyStart + 1, length) >= 0)
                    return JsonText.Unescape(_s.Substring(keyStart + 1, length)) == key;
                return length == key.Length && string.CompareOrdinal(_s, keyStart + 1, key, 0, length) == 0;
            }
        }
    }
}
