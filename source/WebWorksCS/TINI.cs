using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.IO;
using System.Windows.Forms;

using System.Runtime.InteropServices;

namespace WebWorksCS
{
    public class TINI : IDisposable
    {
        //[DllImport("kernel32")]
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        //[DllImport("kernel32")]
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        private bool bDisposed = false;
        private string _FilePath = string.Empty;
        private int Buffer_Size = 2048;
        public string FilePath
        {
            get
            {
                if (_FilePath == null)
                    return string.Empty;
                else
                    return _FilePath;
            }
            set
            {
                if (_FilePath != value)
                    _FilePath = value;
            }
        }

        /// <summary>
        /// 建構子。
        /// </summary>
        /// <param name="path">檔案路徑。</param>      
        public TINI(string path)
        {
            bool File_status = File.Exists(path);
            if (File_status == true)
                _FilePath = path;
            else
                throw new ArgumentException("File is not exist", "path");
        }

        /// <summary>
        /// 解構子。
        /// </summary>
        ~TINI()
        {
            Dispose(false);
        }

        /// <summary>
        /// 釋放資源(程式設計師呼叫)。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); //要求系統不要呼叫指定物件的完成項。
        }

        /// <summary>
        /// 釋放資源(給系統呼叫的)。
        /// </summary>        
        protected virtual void Dispose(bool IsDisposing)
        {
            if (bDisposed)
            {
                return;
            }
            if (IsDisposing)
            {
                //補充：

                //這裡釋放具有實做 IDisposable 的物件(資源關閉或是 Dispose 等..)
                //ex: DataSet DS = new DataSet();
                //可在這邊 使用 DS.Dispose();
                //或是 DS = null;
                //或是釋放 自訂的物件。
                //因為我沒有這類的物件，故意留下這段 code ;若繼承這個類別，
                //可覆寫這個函式。
            }

            bDisposed = true;
        }


        /// <summary>
        /// 設定 KeyValue 值。
        /// </summary>
        /// <param name="IN_Section">Section。</param>
        /// <param name="IN_Key">Key。</param>
        /// <param name="IN_Value">Value。</param>
        public void setKeyValue(string IN_Section, string IN_Key, string IN_Value)
        {
            long Mess = WritePrivateProfileString(IN_Section, IN_Key, IN_Value, this._FilePath);
            //MessageBox.Show(Convert.ToString(Mess));
        }

        public void setKeyValue(string IN_Section, string IN_Key, int IN_Value)
        {
            long Mess = WritePrivateProfileString(IN_Section, IN_Key, Convert.ToString(IN_Value), this._FilePath);
            //MessageBox.Show(Convert.ToString(Mess));
        }

        public void DeleteKeyValue(string IN_Section)
        {
            WritePrivateProfileString(IN_Section, null, null, this._FilePath);
        }

        /// <summary>
        /// 取得 Key 相對的 Value 值。
        /// </summary>
        /// <param name="IN_Section">Section。</param>
        /// <param name="IN_Key">Key。</param>        
        public string getKeyValue(string IN_Section, string IN_Key)
        {
            StringBuilder temp = new StringBuilder(Buffer_Size);
            int i = GetPrivateProfileString(IN_Section, IN_Key, "", temp, Buffer_Size, this._FilePath);           
            return temp.ToString();
        }



        /// <summary>
        /// 取得 Key 相對的 Value 值，若沒有則使用預設值(DefaultValue)。
        /// </summary>
        /// <param name="Section">Section。</param>
        /// <param name="Key">Key。</param>
        /// <param name="DefaultValue">DefaultValue。</param>        
        public string getKeyValue(string Section, string Key, string DefaultValue)
        {
            StringBuilder sbResult = null;
            try
            {
                sbResult = new StringBuilder(Buffer_Size);
                GetPrivateProfileString(Section, Key, "", sbResult, Buffer_Size, this._FilePath);
                return (sbResult.Length > 0) ? sbResult.ToString() : DefaultValue;
            }
            catch
            {
                return string.Empty;
            }
        }

        public int getKeyValue(string Section, string Key, int DefaultValue)
        {
            StringBuilder sbResult = null;
            try
            {
                sbResult = new StringBuilder(Buffer_Size);
                GetPrivateProfileString(Section, Key, "", sbResult, Buffer_Size, this._FilePath);
                return (sbResult.Length > 0) ? Convert.ToInt32(sbResult.ToString()): DefaultValue;
            }
            catch
            {
                return DefaultValue;
            }
        }
    }
}
