using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Plugin
{
    public static class Expression
    {
        // 预编译的正则表达式，匹配形如 {param} 的占位符
        private static readonly Regex ParameterRegex = new Regex(@"\{(.*?)\}", RegexOptions.Compiled);

        /// <summary>
        /// 获取表达式字符串
        /// </summary>
        /// <param name="expression">表达式模板</param>
        /// <param name="values">数组中依次为当前值、前次值、前前次值</param>
        /// <param name="datas">其他参数字典</param>
        /// <returns>替换后的表达式</returns>
        public static string GetExpressionText(this string expression, object[] values, Dictionary<string, object> datas)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));
            if (values == null || values.Length < 3)
                throw new ArgumentException("Values must contain at least three elements", nameof(values));
            if (datas == null)
                throw new ArgumentNullException(nameof(datas));

            try
            {
                // 获取数组中的各个值
                var current = values[0];
                var previous = values[1];
                var prePrevious = values[2];

                // 替换预定义参数
                var result = expression.Trim()
                    .ReplaceExpressionText("raw", current)
                    .ReplaceExpressionText("$v", current)
                    .ReplaceExpressionText("$pv", previous)
                    .ReplaceExpressionText("$ppv", prePrevious);

                // 提取表达式中引用的其他参数名（例如 {param}）
                var parameterNames = ParameterRegex.Matches(result)
                    .Select(m => m.Groups[1].Value)
                    .Distinct();

                // 过滤出字典中与表达式相关的参数
                var filteredDatas = datas.Where(kvp => parameterNames.Contains(kvp.Key));

                // 替换表达式中的其他参数
                foreach (var kvp in filteredDatas)
                {
                    result = result.ReplaceExpressionText($"{{{kvp.Key}}}", kvp.Value);
                }

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// 替换表达式中的占位符为指定的值
        /// </summary>
        /// <param name="expression">原始字符串</param>
        /// <param name="placeholder">占位符名称</param>
        /// <param name="value">替换值</param>
        /// <returns>替换后的字符串</returns>
        public static string ReplaceExpressionText(this string expression, string placeholder, object value)
        {
            if (expression == null)
                return null;

            return expression.Replace(
                placeholder,
                value is bool
                    ? $"Convert.ToBoolean(\"{value}\")"
                    : value?.ToString())
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&")
                .Replace("&quot;", "\""); ;
        }
    }
}
