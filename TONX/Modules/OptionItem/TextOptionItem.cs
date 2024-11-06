using System;
using TONX.Roles.Core;

namespace TONX
{
    public class TextOptionItem : OptionItem
    {
        // 必須情報
        public IntegerValueRule Rule;

        // コンストラクタ
        public TextOptionItem(int id, string name, int defaultValue, TabGroup tab, bool isSingleValue)
        : base(id, name, defaultValue, tab, isSingleValue)
        {
            IsText = true;
        IsHeader = true;
        }
        public static TextOptionItem Create(
            int id, string name, TabGroup tab, bool isSingleValue = false
        )
        {
            return new TextOptionItem(
                id, name, 0, tab, isSingleValue
            );
        }
        public static TextOptionItem Create(
            int id, Enum name, TabGroup tab, bool isSingleValue = false
        )
        {
            return new TextOptionItem(
                id, name.ToString(), 0, tab, isSingleValue
            );
        }
        public static TextOptionItem Create(
            SimpleRoleInfo roleInfo, int idOffset, Enum name, bool isSingleValue = false, OptionItem parent = null
        )
        {
            var opt = new TextOptionItem(
                roleInfo.ConfigId + idOffset, name.ToString(), 0, roleInfo.Tab, isSingleValue
            );
            opt.SetParent(parent ?? roleInfo.RoleOption);
            return opt;
        }

        // Getter
        public override int GetInt() => Rule.GetValueByIndex(CurrentValue);
        public override float GetFloat() => Rule.GetValueByIndex(CurrentValue);
        public override string GetString()
        {
            return Translator.GetString(Name);
        }

        // Setter
        public override void SetValue(int value, bool doSync = true)
        {
        }
    }
}