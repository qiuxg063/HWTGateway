using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WalkingTec.Mvvm.Core;
using WalkingTec.Mvvm.Core.Extensions;
using WalkingTec.Mvvm.Core.Support.Json;

namespace WalkingTec.Mvvm.Core
{
    public class Utils
    {
        private static List<Assembly> _allAssemblies;
        private static List<Type> _allModels;

        public static string GetCurrentComma()
        {
            if (CultureInfo.CurrentUICulture.Name == "zh-cn")
            {
                return "：";
            }
            else
            {
                return ":";
            }
        }

        public static List<Assembly> GetAllAssembly()
        {
            if (_allAssemblies == null)
            {
                _allAssemblies = new List<Assembly>();
                string path = null;
                string singlefile = null;
                try
                {
                    path = Assembly.GetEntryAssembly()?.Location;
                }
                catch { }
                if (string.IsNullOrEmpty(path))
                {
                    singlefile = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    path = Path.GetDirectoryName(singlefile);
                }
                var dir = new DirectoryInfo(Path.GetDirectoryName(path));

                var dlls = dir.GetFiles("*.dll", SearchOption.TopDirectoryOnly);
                string[] systemdll = new string[]
                {
                "Microsoft.",
                "System.",
                "Swashbuckle.",
                "ICSharpCode",
                "Newtonsoft.",
                "Oracle.",
                "Pomelo.",
                "SQLitePCLRaw.",
                "Aliyun.OSS",
                "BouncyCastle.",
                "FreeSql.",
                "Google.Protobuf.dll",
                "Humanizer.dll",
                "IdleBus.dll",
                "K4os.",
                "MySql.Data.",
                "Npgsql.",
                "NPOI.",
                "netstandard",
                "MySqlConnector",
                "VueCliMiddleware"
                };

                var filtered = dlls.Where(x => systemdll.Any(y => x.Name.StartsWith(y)) == false);
                foreach (var dll in filtered)
                {
                    try
                    {
                        AssemblyLoadContext.Default.LoadFromAssemblyPath(dll.FullName);
                    }
                    catch
                    {
                    }
                }
                var dlllist = AssemblyLoadContext.Default.Assemblies.Where(x => systemdll.Any(y => x.FullName.StartsWith(y)) == false).ToList();
                _allAssemblies.AddRange(dlllist);
            }
            return _allAssemblies;
        }

        public static List<Type> GetAllModels()
        {
            if (_allModels == null)
            {
                var modelAsms = Utils.GetAllAssembly();
                var allTypes = new List<Type>();// 所有 DbSet<> 的泛型类型
                                                // 获取所有 DbSet<T> 的泛型类型 T
                foreach (var asm in modelAsms)
                {
                    try
                    {
                        var dcModule = asm.GetExportedTypes().Where(x => typeof(DbContext).IsAssignableFrom(x)).ToList();
                        if (dcModule != null && dcModule.Count > 0)
                        {
                            foreach (var module in dcModule)
                            {
                                foreach (var pro in module.GetProperties())
                                {
                                    if (pro.PropertyType.IsGeneric(typeof(DbSet<>)))
                                    {
                                        if (!allTypes.Contains(pro.PropertyType.GenericTypeArguments[0], new TypeComparer()))
                                        {
                                            allTypes.Add(pro.PropertyType.GenericTypeArguments[0]);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                _allModels = allTypes;
            }
            return _allModels;
        }

        private static List<Type> _allVMs;

        public static List<Type> GetAllVms()
        {
            if (_allVMs == null)
            {
                var modelAsms = Utils.GetAllAssembly();
                var allTypes = new List<Type>();// 所有 DbSet<> 的泛型类型
                                                // 获取所有 DbSet<T> 的泛型类型 T
                foreach (var asm in modelAsms)
                {
                    try
                    {
                        var dcModule = asm.GetExportedTypes().Where(x => typeof(BaseVM).IsAssignableFrom(x)).ToList();
                        allTypes.AddRange(dcModule);
                    }
                    catch { }
                }
                _allVMs = allTypes;
            }
            return _allVMs;
        }

        public static SimpleMenu FindMenu(string url, List<SimpleMenu> menus)
        {
            if (url == null)
            {
                return null;
            }
            url = url.ToLower();
            if (menus == null)
            {
                return null;
            }
            //寻找菜单中是否有与当前判断的url完全相同的
            var menu = menus.Where(x => x.Url != null && x.Url.ToLower() == url).FirstOrDefault();

            //如果没有，抹掉当前url的参数，用不带参数的url比对
            if (menu == null)
            {
                var pos = url.IndexOf("?");
                if (pos > 0)
                {
                    url = url.Substring(0, pos);
                    menu = menus.Where(x => x.Url != null && (x.Url.ToLower() == url || x.Url.ToLower() + "async" == url)).FirstOrDefault();
                }
            }

            //如果还没找到，则判断url是否为/controller/action/id这种格式，如果是则抹掉/id之后再对比
            if (menu == null && url.EndsWith("/index"))
            {
                url = url.Substring(0, url.Length - 6);
                menu = menus.Where(x => x.Url != null && x.Url.ToLower() == url).FirstOrDefault();
            }
            if (menu == null && url.EndsWith("/indexasync"))
            {
                url = url.Substring(0, url.Length - 11);
                menu = menus.Where(x => x.Url != null && x.Url.ToLower() == url).FirstOrDefault();
            }
            return menu;
        }

        public static string GetIdByName(string fieldName)
        {
            return fieldName == null ? "" : fieldName.Replace(".", "_").Replace("[", "_").Replace("]", "_").Replace("-", "minus");
        }

        public static void CheckDifference<T>(IEnumerable<T> oldList, IEnumerable<T> newList, out IEnumerable<T> ToRemove, out IEnumerable<T> ToAdd) where T : TopBasePoco
        {
            List<T> tempToRemove = new List<T>();
            List<T> tempToAdd = new List<T>();
            oldList = oldList ?? new List<T>();
            newList = newList ?? new List<T>();
            foreach (var oldItem in oldList)
            {
                bool exist = false;
                foreach (var newItem in newList)
                {
                    if (oldItem.GetID().ToString() == newItem.GetID().ToString())
                    {
                        exist = true;
                        break;
                    }
                }
                if (exist == false)
                {
                    tempToRemove.Add(oldItem);
                }
            }
            foreach (var newItem in newList)
            {
                bool exist = false;
                foreach (var oldItem in oldList)
                {
                    if (newItem.GetID().ToString() == oldItem.GetID().ToString())
                    {
                        exist = true;
                        break;
                    }
                }
                if (exist == false)
                {
                    tempToAdd.Add(newItem);
                }
            }
            ToRemove = tempToRemove.AsEnumerable();
            ToAdd = tempToAdd.AsEnumerable();
        }

        public static short GetExcelColor(string color)
        {
            var colors = typeof(HSSFColor).GetNestedTypes().ToList();
            foreach (var col in colors)
            {
                var pro = col.GetField("hexString");
                if (pro == null)
                {
                    continue;
                }
                var hex = pro.GetValue(null);
                var rgb = hex.ToString().Split(':');
                for (int i = 0; i < rgb.Length; i++)
                {
                    if (rgb[i].Length > 2)
                    {
                        rgb[i] = rgb[i].Substring(0, 2);
                    }
                }
                int r = Convert.ToInt16(rgb[0], 16);
                int g = Convert.ToInt16(rgb[1], 16);
                int b = Convert.ToInt16(rgb[2], 16);

                if (color.Length == 8)
                {
                    color = color.Substring(2);
                }
                string c1 = color.Substring(0, 2);
                string c2 = color.Substring(2, 2);
                string c3 = color.Substring(4, 2);

                int r1 = Convert.ToInt16(c1, 16);
                int g1 = Convert.ToInt16(c2, 16);
                int b1 = Convert.ToInt16(c3, 16);

                if (r == r1 && g == g1 && b == b1)
                {
                    return (short)col.GetField("index").GetValue(null);
                }
            }
            return HSSFColor.COLOR_NORMAL;
        }

        /// <summary>
        /// 获取Bool类型的下拉框
        /// </summary>
        /// <param name="boolType"></param>
        /// <param name="defaultValue"></param>
        /// <param name="trueText"></param>
        /// <param name="falseText"></param>
        /// <param name="selectText"></param>
        /// <returns></returns>
        public static List<ComboSelectListItem> GetBoolCombo(BoolComboTypes boolType, bool? defaultValue = null, string trueText = null, string falseText = null, string selectText = null)
        {
            List<ComboSelectListItem> rv = new List<ComboSelectListItem>();
            string yesText = "";
            string noText = "";
            switch (boolType)
            {
                case BoolComboTypes.YesNo:
                    yesText = CoreProgram._localizer?["Sys.Yes"];
                    noText = CoreProgram._localizer?["Sys.No"];
                    break;

                case BoolComboTypes.ValidInvalid:
                    yesText = CoreProgram._localizer?["Sys.Valid"];
                    noText = CoreProgram._localizer?["Sys.Invalid"];
                    break;

                case BoolComboTypes.MaleFemale:
                    yesText = CoreProgram._localizer?["Sys.Male"];
                    noText = CoreProgram._localizer?["Sys.Female"];
                    break;

                case BoolComboTypes.HaveNotHave:
                    yesText = CoreProgram._localizer?["Sys.Have"];
                    noText = CoreProgram._localizer?["Sys.NotHave"];
                    break;

                case BoolComboTypes.Custom:
                    yesText = trueText ?? CoreProgram._localizer?["Sys.Yes"];
                    noText = falseText ?? CoreProgram._localizer?["Sys.No"];
                    break;

                default:
                    break;
            }
            ComboSelectListItem yesItem = new ComboSelectListItem()
            {
                Text = yesText,
                Value = "true"
            };
            if (defaultValue == true)
            {
                yesItem.Selected = true;
            }
            ComboSelectListItem noItem = new ComboSelectListItem()
            {
                Text = noText,
                Value = "false"
            };
            if (defaultValue == false)
            {
                noItem.Selected = true;
            }
            if (selectText != null)
            {
                rv.Add(new ComboSelectListItem { Text = selectText, Value = "" });
            }
            rv.Add(yesItem);
            rv.Add(noItem);
            return rv;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string ZipAndBase64Encode(string input)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(input);
            MemoryStream inputms = new MemoryStream(buffer);
            MemoryStream outputms = new MemoryStream();
            using (GZipStream zip = new GZipStream(outputms, CompressionMode.Compress))
            {
                inputms.CopyTo(zip);
            }
            byte[] rv = outputms.ToArray();
            inputms.Dispose();
            return Convert.ToBase64String(rv);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string UnZipAndBase64Decode(string input)
        {
            byte[] inputstr = Convert.FromBase64String(input);
            MemoryStream inputms = new MemoryStream(inputstr);
            MemoryStream outputms = new MemoryStream();
            using (GZipStream zip = new GZipStream(inputms, CompressionMode.Decompress))
            {
                zip.CopyTo(outputms);
            }
            byte[] rv = outputms.ToArray();
            outputms.Dispose();
            return Encoding.UTF8.GetString(rv);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string EncodeScriptJson(string input)
        {
            if (input == null)
            {
                return "";
            }
            else
            {
                return input.Replace(Environment.NewLine, "").Replace("\"", "\\\\\\\"").Replace("'", "\\'");
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="path"></param>
        public static void DeleteFile(string path)
        {
            try
            {
                System.IO.File.Delete(path);
            }
            catch { }
        }

        #region 格式化文本  add by wuwh 2014.6.12

        /// <summary>
        /// 格式化文本
        /// </summary>
        /// <param name="text">要格式化的字符串</param>
        /// <param name="isCode">是否是纯代码</param>
        /// <returns></returns>
        public static string FormatText(string text, bool isCode = false)
        {
            if (isCode)
            {
                return FormatCode(text);
            }
            else
            {
                #region 截取需要格式化的代码段

                List<int> listInt = new List<int>();
                int index = 0;
                int _index;
                while (true)
                {
                    _index = text.IndexOf("&&", index);
                    index = _index + 1;
                    if (_index >= 0 && _index <= text.Length)
                    {
                        listInt.Add(_index);
                    }
                    else
                    {
                        break;
                    }
                }

                List<string> listStr = new List<string>();
                for (int i = 0; i < listInt.Count; i++)
                {
                    string temp = text.Substring(listInt[i] + 2, listInt[i + 1] - listInt[i] - 2);

                    listStr.Add(temp);
                    i++;
                }

                #endregion 截取需要格式化的代码段

                #region 格式化代码段

                //先将 <  >以及空格替换掉，防止下面替换出现 html标签后出现问题
                for (int i = 0; i < listStr.Count; i++)
                {
                    //将 &&代码&&  替换成&&1&&
                    text = text.Replace("&&" + listStr[i] + "&&", FormatCode(listStr[i]));
                }

                #endregion 格式化代码段

                return text;
            }
        }

        #endregion 格式化文本  add by wuwh 2014.6.12

        #region 格式化代码  edit by wuwh

        /// <summary>
        /// 格式化代码
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static string FormatCode(string code)
        {
            //先将 <  >以及空格替换掉，防止下面替换出现 html标签后出现问题
            code = code.Replace("<", "&lt;").Replace(">", "&gt;").Replace(" ", "&nbsp;");
            string csKeyWords = "abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|from|get|goto|group|if|implicit|in|int|interface|internal|into|is|join|let|lock|long|namespace|new|null|object|operator|orderby|out|override|params|partial|private|protected|public|readonly|ref|return|sbyte|sealed|select|set|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|value|var|virtual|void|volatile|where|while|yield";

            string r1 = "(#if DBG[\\s\\S]+?#endif)";
            string r2 = "(#[a-z ]*)";
            string r3 = "(///\\ *<[/\\w]+>)";
            string r4 = "(/\\*[\\s\\S]*?\\*/)";//匹配三杠注释
            string r5 = "(//.*)";//匹配双杠注释
            string r6 = @"(@?"".*?"")";//匹配字符串
            string r7 = "('.*?')";//匹配字符串
            string r8 = "\\b(" + csKeyWords + ")\\b";//匹配关键字
            //string r9 = "class&nbsp;(.+)&nbsp;";//匹配类
            //string r10 = "&lt;(.+)&gt;";//匹配泛型类

            string rs = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}", r1, r2, r3, r4, r5, r6, r7, r8);
            //string rs = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}", r1, r2, r3, r4, r5, r6, r7, r8, r9,r10);

            //<font color=#44C796>$9$10</font>
            string rr = "<font color=#808080>$1$2$3</font><font color=#008000>$4$5</font><font color=#A31515>$6$7</font><font color=#0000FF>$8</font>";

            Regex re = new Regex(rs, RegexOptions.None);
            code = Regex.Replace(code, rs, rr);
            //替换换行符"\r\n"   以及"\r"  "\n"
            code = code.Replace("\r\n", "<br>").Replace("\n", "").Replace("\r", "<br>");
            //取消空标签
            //|<font color=#44C796></font>C#类的颜色
            code = Regex.Replace(code, "<font color=#808080></font>|<font color=#008000></font>|<font color=#A31515></font>|<font color=#0000FF></font>", "");

            return code;
        }

        #endregion 格式化代码  edit by wuwh

        #region 读取txt文件

        /// <summary>
        /// 读取文件
        /// </summary>
        /// <param name="path">文件路径绝对</param>
        /// <returns></returns>
        public static string ReadTxt(string path)
        {
            string result = string.Empty;

            if (File.Exists(path))
            {
                using (Stream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (TextReader sr = new StreamReader(fs, UnicodeEncoding.UTF8))
                    {
                        result = sr.ReadToEnd();
                    }
                }
            }

            return result;
        }

        #endregion 读取txt文件

        /// <summary>
        /// 得到目录下所有文件
        /// </summary>
        /// <param name="dirpath"></param>
        /// <returns></returns>
        public static List<string> GetAllFileName(string dirpath)
        {
            DirectoryInfo dir = new DirectoryInfo(dirpath);
            var files = dir.GetFileSystemInfos();
            return files.Select(x => x.Name).ToList();
        }

        #region add by wuwh 2014.10.18  递归获取目录下所有文件

        /// <summary>
        /// 递归获取目录下所有文件
        /// </summary>
        /// <param name="dirPath"></param>
        /// <param name="allFiles"></param>
        /// <returns></returns>
        public static List<string> GetAllFilePathRecursion(string dirPath, List<string> allFiles)
        {
            if (allFiles == null)
            {
                allFiles = new List<string>();
            }
            string[] subPaths = Directory.GetDirectories(dirPath);
            foreach (var item in subPaths)
            {
                GetAllFilePathRecursion(item, allFiles);
            }
            allFiles.AddRange(Directory.GetFiles(dirPath).ToList());

            return allFiles;
        }

        #endregion add by wuwh 2014.10.18  递归获取目录下所有文件

        /// <summary>
        /// ConvertToColumnXType
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string ConvertToColumnXType(Type type)
        {
            if (type == typeof(bool) || type == typeof(bool?))
            {
                return "checkcolumn";
            }
            else if (type == typeof(DateTime) || type == typeof(DateTime?))
            {
                return "datecolumn";
            }
            else if (type == typeof(decimal) || type == typeof(decimal?) || type == typeof(double) || type == typeof(double?) || type == typeof(int) || type == typeof(int?) || type == typeof(long) || type == typeof(long?))
            {
                return "numbercolumn";
            }
            return "textcolumn";
        }

        public static string GetCS(string cs, string mode, Configs config)
        {
            if (cs == null)
            {
                return null;
            }

            if (config.Connections.Any(x => x.Key.ToLower() == cs.ToLower()) == false)
            {
                cs = "default";
            }
            int index = cs.LastIndexOf("_");
            if (index > 0)
            {
                cs = cs.Substring(0, index);
            }
            if (mode?.ToLower() == "read")
            {
                var reads = config.Connections.Where(x => x.Key.StartsWith(cs + "_")).Select(x => x.Key).ToList();
                if (reads.Count > 0)
                {
                    Random r = new Random();
                    var v = r.Next(0, reads.Count);
                    cs = reads[v];
                }
            }
            return cs;
        }

        public static string GetUrlByFileAttachmentId(IDataContext dc, Guid? fileAttachmentId, bool isIntranetUrl = false, string urlHeader = null)
        {
            string url = string.Empty;
            var fileAttachment = dc.Set<FileAttachment>().Where(x => x.ID == fileAttachmentId.Value).FirstOrDefault();
            if (fileAttachment != null)
            {
                url = "/_Framework/GetFile/" + fileAttachmentId.ToString();
            }
            return url;
        }

        #region 加解密

        private const string AesCipherPrefix = "AES:";
        private const string AesGcmCipherPrefix = "GCM:";
        private const int AesGcmNonceSize = 12;
        private const int AesGcmTagSize = 16;

        /// <summary>
        /// 通过密钥将内容加密
        /// 新版本使用 AES-GCM；为了兼容旧数据，密文带有 GCM: 前缀。
        /// </summary>
        /// <param name="stringToEncrypt">要加密的字符串</param>
        /// <param name="encryptKey">加密密钥</param>
        /// <returns></returns>
        public static string EncryptString(string stringToEncrypt, string encryptKey)
        {
            if (string.IsNullOrEmpty(stringToEncrypt))
            {
                return "";
            }

            try
            {
                byte[] key = GetAesKey(encryptKey);
                byte[] nonce = RandomNumberGenerator.GetBytes(AesGcmNonceSize);
                byte[] plainBytes = Encoding.UTF8.GetBytes(stringToEncrypt);
                byte[] cipherBytes = new byte[plainBytes.Length];
                byte[] tag = new byte[AesGcmTagSize];

                using var aes = new AesGcm(key, AesGcmTagSize);
                aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

                return AesGcmCipherPrefix + Convert.ToBase64String(nonce) + "." +
                       Convert.ToBase64String(tag) + "." +
                       Convert.ToBase64String(cipherBytes);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 通过密钥讲内容解密
        /// 先解 AES-GCM；再兼容旧 AES-CBC；最后兼容 DES。
        /// </summary>
        /// <param name="stringToDecrypt">要解密的字符串</param>
        /// <param name="encryptKey">密钥</param>
        /// <returns></returns>
        public static string DecryptString(string stringToDecrypt, string encryptKey)
        {
            if (string.IsNullOrEmpty(stringToDecrypt))
            {
                return "";
            }

            if (stringToDecrypt.StartsWith(AesGcmCipherPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return DecryptAesGcmString(stringToDecrypt.Substring(AesGcmCipherPrefix.Length), encryptKey);
            }

            if (stringToDecrypt.StartsWith(AesCipherPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return DecryptAesString(stringToDecrypt.Substring(AesCipherPrefix.Length), encryptKey);
            }

            return DecryptLegacyDesString(stringToDecrypt, encryptKey);
        }

        /// <summary>
        /// AES-GCM 解密
        /// </summary>
        private static string DecryptAesGcmString(string stringToDecrypt, string encryptKey)
        {
            try
            {
                var parts = stringToDecrypt.Split('.', StringSplitOptions.None);
                if (parts.Length != 3)
                {
                    return "";
                }

                byte[] nonce = Convert.FromBase64String(parts[0]);
                byte[] tag = Convert.FromBase64String(parts[1]);
                byte[] cipherBytes = Convert.FromBase64String(parts[2]);
                byte[] plainBytes = new byte[cipherBytes.Length];
                byte[] key = GetAesKey(encryptKey);

                using var aes = new AesGcm(key, AesGcmTagSize);
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 旧版 AES-CBC 解密，保留用于兼容历史密文
        /// </summary>
        private static string DecryptAesString(string stringToDecrypt, string encryptKey)
        {
            try
            {
                byte[] allBytes = Convert.FromBase64String(stringToDecrypt.Replace(" ", "+"));
                if (allBytes.Length <= 16)
                {
                    return "";
                }

                byte[] iv = new byte[16];
                byte[] cipherBytes = new byte[allBytes.Length - 16];
                Buffer.BlockCopy(allBytes, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(allBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

                byte[] key = GetAesKey(encryptKey);

                using var aes = Aes.Create();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Key = key;
                aes.IV = iv;

                using var decryptStream = new MemoryStream(cipherBytes);
                using var cryptoStream = new CryptoStream(decryptStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var reader = new StreamReader(cryptoStream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 旧版 DES 解密，保留用于兼容历史密文
        /// </summary>
        private static string DecryptLegacyDesString(string stringToDecrypt, string encryptKey)
        {
            try
            {
                byte[] bytIn = Convert.FromBase64String(stringToDecrypt.Replace(" ", "+"));
                using MemoryStream decryptStream = new MemoryStream();
                using CryptoStream encStream = new CryptoStream(decryptStream, GenerateDESCryptoServiceProvider(encryptKey).CreateDecryptor(), CryptoStreamMode.Write);

                encStream.Write(bytIn, 0, bytIn.Length);
                encStream.FlushFinalBlock();
                return Encoding.Default.GetString(decryptStream.ToArray());
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 从密钥生成 AES 256 位 Key
        /// </summary>
        private static byte[] GetAesKey(string key)
        {
            key ??= string.Empty;
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        }

        private static DES GenerateDESCryptoServiceProvider(string key)
        {
            var dCrypter = DES.Create();

            string sTemp;
            if (dCrypter.LegalKeySizes.Length > 0)
            {
                int moreSize = dCrypter.LegalKeySizes[0].MinSize;
                while (key.Length > 8)
                {
                    key = key.Substring(0, 8);
                }
                sTemp = key.PadRight(moreSize / 8, ' ');
            }
            else
            {
                sTemp = key;
            }
            byte[] bytKey = UTF8Encoding.UTF8.GetBytes(sTemp);

            dCrypter.Key = bytKey;
            dCrypter.IV = bytKey;

            return dCrypter;
        }

        #endregion 加解密

        #region 密码哈希（PBKDF2）

        private const string PasswordHashPrefix = "PBKDF2";
        private const int PasswordHashIterations = 100000;
        private const int PasswordSaltSize = 16;
        private const int PasswordKeySize = 32;

        /// <summary>
        /// 兼容旧调用点：当前返回 PBKDF2 哈希字符串，沿用原方法名以减少改动面
        /// </summary>
        /// <param name="str"></param>
        /// <returns>PBKDF2 哈希字符串</returns>
        /*
        public static string GetMD5String(string str)
        {
            return HashPassword(str);
        }
        */

        /// <summary>
        /// 使用 PBKDF2 生成密码哈希
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <returns>PBKDF2$迭代次数$SaltBase64$HashBase64</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return "";
            }

            byte[] salt = RandomNumberGenerator.GetBytes(PasswordSaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                PasswordHashIterations,
                HashAlgorithmName.SHA256,
                PasswordKeySize);

            return string.Join("$",
                PasswordHashPrefix,
                PasswordHashIterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        /// <summary>
        /// 验证密码，支持 PBKDF2 和旧 MD5 格式
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="storedHash">数据库中的密码哈希</param>
        /// <returns>验证是否通过</returns>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            if (TryParsePbkdf2Hash(storedHash, out int iterations, out byte[] salt, out byte[] expectedHash))
            {
                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }

            if (IsLegacyMd5Hash(storedHash))
            {
                return string.Equals(ComputeLegacyMd5Hex(password), storedHash, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// 判断是否为旧版 MD5 32 位哈希
        /// </summary>
        public static bool IsLegacyMd5Hash(string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash) || storedHash.Length != 32)
            {
                return false;
            }

            for (int i = 0; i < storedHash.Length; i++)
            {
                char c = storedHash[i];
                if ((c >= '0' && c <= '9') == false &&
                    (c >= 'a' && c <= 'f') == false &&
                    (c >= 'A' && c <= 'F') == false)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParsePbkdf2Hash(string storedHash, out int iterations, out byte[] salt, out byte[] hash)
        {
            iterations = 0;
            salt = Array.Empty<byte>();
            hash = Array.Empty<byte>();

            var parts = storedHash.Split('$', StringSplitOptions.None);
            if (parts.Length != 4)
            {
                return false;
            }

            if (parts[0].Equals(PasswordHashPrefix, StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            if (int.TryParse(parts[1], out iterations) == false || iterations <= 0)
            {
                return false;
            }

            try
            {
                salt = Convert.FromBase64String(parts[2]);
                hash = Convert.FromBase64String(parts[3]);
                return salt.Length > 0 && hash.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeLegacyMd5Hex(string str)
        {
            if (str == null)
            {
                return "";
            }

            return MD5String(Encoding.UTF8.GetBytes(str));
        }

        #endregion 密码哈希（PBKDF2）

        #region MD5加密

        /// 字符串MD5加密
        /// </summary>
        /// <param name="str"></param>
        /// <returns>返回大写32位MD5值</returns>
        public static string GetMD5String(string str)
        {
            if (str == null)
            {
                return "";
            }
            byte[] buffer = Encoding.UTF8.GetBytes(str);

            return MD5String(buffer);
        }

        /// <summary>
        /// 流MD5加密
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static string GetMD5Stream(Stream stream)
        {
            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return MD5String(buffer);
        }

        /// <summary>
        /// 文件MD5加密
        /// </summary>
        /// <param name="path"></param>
        /// <returns>返回大写32位MD5值</returns>
        public static string GetMD5File(string path)
        {
            if (File.Exists(path))
            {
                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    return GetMD5Stream(fs);
                }
            }
            else
            {
                return string.Empty;
            }
        }

        private static string MD5String(byte[] buffer)
        {
            var md5 = MD5.Create();
            byte[] cryptBuffer = md5.ComputeHash(buffer);
            StringBuilder sb = new StringBuilder();
            foreach (byte item in cryptBuffer)
            {
                sb.Append(item.ToString("X2"));
            }
            return sb.ToString();
        }

        #endregion MD5加密

        #region PBKDF2文件完整性校验

        /// <summary>
        /// PBKDF2默认迭代次数
        /// </summary>
        private const int PBKDF2DefaultIterations = 100000;

        /// <summary>
        /// PBKDF2默认Salt长度
        /// </summary>
        private const int PBKDF2DefaultSaltSize = 16;

        /// <summary>
        /// PBKDF2默认输出长度
        /// </summary>
        private const int PBKDF2DefaultKeySize = 32;

        /// <summary>
        /// 生成PBKDF2用的Salt，返回Base64字符串
        /// </summary>
        /// <param name="saltSize">Salt字节长度</param>
        /// <returns>Base64 Salt</returns>
        public static string CreatePBKDF2Salt(int saltSize = PBKDF2DefaultSaltSize)
        {
            if (saltSize <= 0)
            {
                return string.Empty;
            }

            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(saltSize));
        }

        /// <summary>
        /// 计算文件的PBKDF2完整性值
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="salt">Salt，建议使用Base64字符串</param>
        /// <param name="iterations">迭代次数</param>
        /// <param name="keySize">输出长度，字节数</param>
        /// <returns>大写Hex字符串</returns>
        public static string GetPBKDF2File(string path, string salt, int iterations = PBKDF2DefaultIterations, int keySize = PBKDF2DefaultKeySize)
        {
            if (string.IsNullOrWhiteSpace(path) || File.Exists(path) == false)
            {
                return string.Empty;
            }

            using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return GetPBKDF2Stream(fs, salt, iterations, keySize);
        }

        /// <summary>
        /// 计算流的PBKDF2完整性值
        /// </summary>
        /// <param name="stream">输入流</param>
        /// <param name="salt">Salt，建议使用Base64字符串</param>
        /// <param name="iterations">迭代次数</param>
        /// <param name="keySize">输出长度，字节数</param>
        /// <returns>大写Hex字符串</returns>
        public static string GetPBKDF2Stream(Stream stream, string salt, int iterations = PBKDF2DefaultIterations, int keySize = PBKDF2DefaultKeySize)
        {
            if (stream == null || string.IsNullOrWhiteSpace(salt))
            {
                return string.Empty;
            }

            if (iterations <= 0 || keySize <= 0)
            {
                return string.Empty;
            }

            byte[] saltBytes = GetPBKDF2SaltBytes(salt);
            if (saltBytes.Length == 0)
            {
                return string.Empty;
            }

            long? originalPosition = null;

            try
            {
                if (stream.CanSeek)
                {
                    originalPosition = stream.Position;
                    stream.Position = 0;
                }

                using var sha256 = SHA256.Create();
                byte[] fileHash = sha256.ComputeHash(stream);

                byte[] derived = Rfc2898DeriveBytes.Pbkdf2(
                    fileHash,
                    saltBytes,
                    iterations,
                    HashAlgorithmName.SHA256,
                    keySize);

                return Convert.ToHexString(derived);
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                if (stream.CanSeek && originalPosition.HasValue)
                {
                    stream.Position = originalPosition.Value;
                }
            }
        }

        /// <summary>
        /// 验证文件PBKDF2完整性值
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="expectedHash">期望值</param>
        /// <param name="salt">Salt，建议使用Base64字符串</param>
        /// <param name="iterations">迭代次数</param>
        /// <param name="keySize">输出长度，字节数</param>
        /// <returns>是否一致</returns>
        public static bool VerifyPBKDF2File(string path, string expectedHash, string salt, int iterations = PBKDF2DefaultIterations, int keySize = PBKDF2DefaultKeySize)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                return false;
            }

            var actualHash = GetPBKDF2File(path, salt, iterations, keySize);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 验证流PBKDF2完整性值
        /// </summary>
        /// <param name="stream">输入流</param>
        /// <param name="expectedHash">期望值</param>
        /// <param name="salt">Salt，建议使用Base64字符串</param>
        /// <param name="iterations">迭代次数</param>
        /// <param name="keySize">输出长度，字节数</param>
        /// <returns>是否一致</returns>
        public static bool VerifyPBKDF2Stream(Stream stream, string expectedHash, string salt, int iterations = PBKDF2DefaultIterations, int keySize = PBKDF2DefaultKeySize)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                return false;
            }

            var actualHash = GetPBKDF2Stream(stream, salt, iterations, keySize);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 解析Salt字符串，优先按Base64处理，失败后按普通UTF8字符串处理
        /// </summary>
        private static byte[] GetPBKDF2SaltBytes(string salt)
        {
            if (string.IsNullOrWhiteSpace(salt))
            {
                return Array.Empty<byte>();
            }

            try
            {
                return Convert.FromBase64String(salt);
            }
            catch
            {
                return Encoding.UTF8.GetBytes(salt);
            }
        }

        #endregion PBKDF2文件完整性校验

        /// <summary>
        /// 重新处理 返回所有ispage模块
        /// </summary>
        /// <param name="modules"></param>
        /// <param name="submit">是否需要action</param>
        /// <returns></returns>
        public static List<SimpleModule> ResetModule(List<SimpleModule> modules, bool submit = true)
        {
            var m = modules.Select(x => new SimpleModule
            {
                ActionDes = x.ActionDes,
                Actions = x.Actions.Select(y => new SimpleAction
                {
                    ActionDes = y.ActionDes,
                    ActionName = y.ActionName,
                    Url = y.Url,
                    MethodName = y.MethodName,
                    IgnorePrivillege = y.IgnorePrivillege,
                    ID = y.ID,
                    Module = y.Module,
                    ModuleId = y.ModuleId,
                    Parameter = y.Parameter,
                    ParasToRunTest = y.ParasToRunTest
                }).ToList(),
                Area = x.Area,
                AreaId = x.AreaId,
                ClassName = x.ClassName,
                _name = x._name,
                ID = x.ID,
                IgnorePrivillege = x.IgnorePrivillege,
                IsApi = x.IsApi,
                ModuleName = x.ModuleName,
                NameSpace = x.NameSpace,
            }).ToList();
            var mCount = m.Count;
            var toRemove = new List<SimpleModule>();
            for (int i = 0; i < mCount; i++)
            {
                var pages = m[i].Actions?.Where(x => x.ActionDes?.IsPage == true).ToList();
                if (pages != null)
                {
                    for (int j = 0; j < pages.Count; j++)
                    {
                        if (j == 0 && !m[i].Actions.Any(x => x.MethodName.ToLower() == "index"))
                        {
                            m.Add(new SimpleModule
                            {
                                ModuleName = pages[j].ActionDes._localizer[pages[j].ActionDes.Description],
                                NameSpace = m[i].NameSpace,
                                ClassName = pages[j].MethodName,
                                Actions = m[i].Actions,
                                Area = m[i].Area
                            });
                            if (submit)
                                m[i].Actions.Remove(pages[j]);
                            toRemove.Add(m[i]);
                        }
                        else
                        {
                            if (pages[j].MethodName.ToLower() != "index")
                            {
                                m.Add(new SimpleModule
                                {
                                    ModuleName = pages[j].ActionDes._localizer[pages[j].ActionDes.Description],
                                    NameSpace = m[i].NameSpace,
                                    ClassName = pages[j].Module.ClassName + pages[j].MethodName,
                                    Actions = submit ? new List<SimpleAction>() : new List<SimpleAction>() { pages[j] },
                                    Area = m[i].Area
                                });
                                m[i].Actions.Remove(pages[j]);
                            }
                        }
                    }
                }
            }
            toRemove.ForEach(x => m.Remove(x));
            return m;
        }
    }
}

//使用方式
//生成文件完整性值
/*
var salt = Utils.CreatePBKDF2Salt();
var hash = Utils.GetPBKDF2File(filePath, salt);
*/

//校验文件是否被篡改
/*
var ok = Utils.VerifyPBKDF2File(filePath, storedHash, storedSalt);
*/