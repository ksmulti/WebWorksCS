using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Threading;
using System.Globalization;
using System.Runtime.Serialization.Formatters.Binary;

namespace WebWorksCS
{
    public partial class MainForm : Form
    {
        private delegate void myTextUICallBack(string myStr, Control ctl);
        private delegate void myRichTextBoxAppendUICallBack(string myStr, RichTextBox ctl);
        private delegate void myProgressBarValueUICallBack(int myInt, ProgressBar ProgressBarCtl);
        private void myTextUI(string myStr, Control ctl)
        {
            if (this.InvokeRequired)
            {
                myTextUICallBack myUpdate = new myTextUICallBack(myTextUI);
                this.Invoke(myUpdate, myStr, ctl);
            }
            else
            {
                ctl.Text = myStr;
            }
        }

        private void myRichTextBoxAppendUI(string myStr, RichTextBox ctl)
        {
            if (this.InvokeRequired)
            {
                myRichTextBoxAppendUICallBack myUpdate = new myRichTextBoxAppendUICallBack(myRichTextBoxAppendUI);
                this.Invoke(myUpdate, myStr, ctl);
            }
            else
            {
                ctl.AppendText(myStr);
            }
        }

        private void myProgressBarValueUI(int myInt, ProgressBar ProgressBarCtl)
        {
            if (this.InvokeRequired)
            {
                myProgressBarValueUICallBack myUpdate = new myProgressBarValueUICallBack(myProgressBarValueUI);
                this.Invoke(myUpdate, myInt, ProgressBarCtl);
            }
            else
            {
                ProgressBarCtl.Value = myInt;
            }
        }

        enum enumURL_Type { QUESTION_MARK, NORMAL };
        enum enumPostContentType { QUESTION_MARK, POSTCONTENT, POSTCONTENT_X };
        enum enumHttpMethod { POST, GET, POST_MULTIPART };
        enum enumHttpFunction 
        {
            PostData = 1,
            PostData_MultiPart = 2,
            StrSearch = 3,
            GetString = 4,
            GetData = 5,
            PostData2 = 6,
            PostData3 = 7,
            StrSearch_POST = 8,
            GetString_POST = 9
        };

        private TINI WebWorks_ini;
        private string sDestIP;
        private List<string> aWorkSeq;
        private int WorkStage;
        private bool InProcessAuth;
        private string Authorization;
        private const int BaseFieldNumber = 30;
        //private bool AutoCookie;
        //private CookieContainer myCookie = new CookieContainer();        

        public MainForm()
        {
            InitializeComponent();
        }

        /*
        Program Flow:
         * MainForm_Load -> MainForm_Shown -> WebActionSequence(do Workseq) -> 各function(PostData, PostData_MultiPart, ...)
         */

        private void MainForm_Load(object sender, EventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            //讀取WebWorks.ini
            try
            {
                WebWorks_ini = new TINI(Path.Combine(Application.StartupPath, "WebWorks.ini"));
            }
            catch (Exception ex)
            {
                PutErrorMessage(ex);
                Environment.Exit(Environment.ExitCode);
            }            
        }        

        private void MainForm_Shown(object sender, EventArgs e)
        {
            string sWorkSeq;            

            WebWorks_ini.DeleteKeyValue("ReturnedData");
            WebWorks_ini.setKeyValue("ErrorMessage", "Err", "");
            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "1");
            //讀取DestIP, Workseq
            sDestIP = WebWorks_ini.getKeyValue("Configuration", "DestIP", "");
            if (sDestIP == "")
            {                
                PutErrorMessage("DestIP in Webwork.ini is empty");
                WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                Environment.Exit(Environment.ExitCode);
            }
            sDestIP = "http://" + sDestIP;

            bool HttpsEnable = Convert.ToBoolean(WebWorks_ini.getKeyValue("Configuration", "HttpsEnable", 0));
            if (HttpsEnable)
            {                   
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
            }                
                    
            //設定是否進入網頁時認證
            //Add Http header (Authorization: Basic XXXXXXXX) 
            InProcessAuth = Convert.ToBoolean(WebWorks_ini.getKeyValue("Configuration", "InProcessAuth", 0));
            if (InProcessAuth)
            {                    
                string Username = WebWorks_ini.getKeyValue("Configuration", "Request.Username", "");
                string Password = WebWorks_ini.getKeyValue("Configuration", "Request.Password", "");
                string UsernamePassword = Username + ":" + Password;
                Authorization = "Authorization" + ": Basic " + Convert.ToBase64String(new ASCIIEncoding().GetBytes(UsernamePassword));
            }

            //設定是否使用Auto Cookie
            //AutoCookie = Convert.ToBoolean(WebWorks_ini.getKeyValue("Configuration", "AutoCookie", 0));            

            //Load WorkSeq from INI
            sWorkSeq = WebWorks_ini.getKeyValue("Configuration", "WorkSeq", "");                
            aWorkSeq = new List<string>(sWorkSeq.Split(','));                
            for (int i=0; i<aWorkSeq.Count; i++)                
                aWorkSeq[i] = aWorkSeq[i].Trim();                    
                
            if (aWorkSeq[0] == "")
            {                    
                PutErrorMessage("WorkSeq in Webwork.ini is empty");
                WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                Environment.Exit(Environment.ExitCode);
            }
            else
            {                    
                progressBar1.Maximum = aWorkSeq.Count;
                WorkStage = 0;
                //Run the sequence of Web action in other thread
                ThreadStart myRun = new ThreadStart(WebActionSequence);
                Thread myThread = new Thread(myRun); 
                myThread.Start();
            }                 
        }       

        private void WebActionSequence()
        {            
            string sTemp;
            int iTemp;
            int SleepTime;
            
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            while (true)
            {
                sTemp = WebWorks_ini.getKeyValue("Configuration", "Work" + aWorkSeq[WorkStage] + ".Msg", "The WebWorks is executing the Work" + aWorkSeq[WorkStage] + "......");
                myRichTextBoxAppendUI(sTemp + Environment.NewLine, richTextBox_Status);                
                SleepTime = WebWorks_ini.getKeyValue("Configuration", "Work" + aWorkSeq[WorkStage] + ".Sleep", 0) + 1;
                int FuncType = WebWorks_ini.getKeyValue("Configuration", "Work" + aWorkSeq[WorkStage] + ".Func", -1);
                /*
                System.DateTime TimeOri = new System.DateTime();
                TimeOri = System.DateTime.Now;
                Thread.Sleep(2000);
                System.DateTime TimeNow = new System.DateTime();
                TimeNow = System.DateTime.Now;
                TimeSpan TimeDiff = TimeNow - TimeOri;
                MessageBox.Show(TimeDiff.TotalSeconds.ToString());
                */
                switch (FuncType)
                {
                    /*
                     * WorkX.Func=1
                     * Send a Http POST packet
                     * Use '?' as a delimiter of Web Address and POST command
                     * Ex: /set.cgi?n=WIFI_5G_SSID&v=5G123
                     * Web Address => /set.cgi
                     * POST command => n=WIFI_5G_SSID&v=5G123
                     */
                    case (int)enumHttpFunction.PostData:
                        if (!SendHttpReqAndGetResp("Work" + aWorkSeq[WorkStage], enumHttpFunction.PostData, enumHttpMethod.POST))
                        {
                            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                            Environment.Exit(Environment.ExitCode);
                        }
                        break;
                    /*
                     * WorkX.Func=2
                     * Send a Http POST MultiPart packet         
                     */
                    case (int)enumHttpFunction.PostData_MultiPart:                        
                        if (!SendHttpReqAndGetResp("Work" + aWorkSeq[WorkStage], enumHttpFunction.PostData_MultiPart, enumHttpMethod.POST_MULTIPART))
                        {
                            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                            Environment.Exit(Environment.ExitCode);
                        }
                        break;
                    /*
                     * WorkX.Func=3
                     * Send a Http Get packet
                     * And compare the keyword
                     */
                    case (int)enumHttpFunction.StrSearch: 
                       
                        if (!SendHttpReqAndGetResp("Work" + aWorkSeq[WorkStage], enumHttpFunction.StrSearch, enumHttpMethod.GET))
                        {
                            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                            Environment.Exit(Environment.ExitCode);
                        }
                        break;
                    /*
                     * WorkX.Func=4
                     * Send a Http Get packet
                     * And Get the target string after the keyword
                     */
                    case (int)enumHttpFunction.GetString:                          
                        if (!SendHttpReqAndGetResp("Work" + aWorkSeq[WorkStage], enumHttpFunction.GetString, enumHttpMethod.GET))
                        {
                            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                            Environment.Exit(Environment.ExitCode);
                        }
                        break;
                    /*
                     * WorkX.Func=5
                     * Send a Http Get packet
                     * And do nothing, only check the connection of the web address
                     */
                    case (int)enumHttpFunction.GetData:                       
                        if (!SendHttpReqAndGetResp("Work" + aWorkSeq[WorkStage], enumHttpFunction.GetData, enumHttpMethod.GET))
                        {
                            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                            Environment.Exit(Environment.ExitCode);
                        }
                        break;
                    /*
                     * WorkX.Func=6
                     * Send a Http POST packet
                     * Use "WorkX.PostContent" to save POST command         
                     */
                    case (int)enumHttpFunction.PostData2:
                        if (!SendHttpReqAndGetResp("Work" + aWorkSeq[WorkStage], enumHttpFunction.PostData2, enumHttpMethod.POST))
                        {
                            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                            Environment.Exit(Environment.ExitCode);
                        }
                        break;
                    /*
                     * WorkX.Func=7
                     * Send a Http POST packet
                     * Use "WorkX.PostContentX" to save POST command, X = 1, 2, 3, ...  
                     * The final POST command will combine them
                     * EX:  WorkX.PostContent1 = AAA&
                     *      WorkX.PostContent2 = BBB&
                     *      WorkX.PostContent3 = CCC
                     *      ...
                     * The POST command will be "AAA&BBB&CCC"
                     * The method is used because string length of Procomm Plus is not more 256
                     */
                    case (int)enumHttpFunction.PostData3:                        
                        if (!SendHttpReqAndGetResp("Work" + aWorkSeq[WorkStage], enumHttpFunction.PostData3, enumHttpMethod.POST))
                        {
                            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                            Environment.Exit(Environment.ExitCode);
                        }
                        break;
                    /*
                     * WorkX.Func=8
                     * Send a Http POST packet
                     * And compare the keyword as function "StrSearch", but this is a POST method
                     * Use "WorkX.PostContentX" to save POST command as function "PostData3"
                     */
                    case (int)enumHttpFunction.StrSearch_POST:                        
                        if (!SendHttpReqAndGetResp("Work" + aWorkSeq[WorkStage], enumHttpFunction.StrSearch_POST, enumHttpMethod.POST))
                        {
                            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                            Environment.Exit(Environment.ExitCode);
                        }
                        break;
                    /*
                     * WorkX.Func=9
                     * Send a Http POST packet
                     * And Get the target string after the keyword as function "GetString", but this is a POST method
                     * Use "WorkX.PostContentX" to save POST command as function "PostData3"
                     */
                    case (int)enumHttpFunction.GetString_POST:                       
                        if (!SendHttpReqAndGetResp("Work" + aWorkSeq[WorkStage], enumHttpFunction.GetString_POST, enumHttpMethod.POST))
                        {
                            WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                            Environment.Exit(Environment.ExitCode);
                        }
                        break;
                    default:                                        
                        PutErrorMessage("-> Work" + aWorkSeq[WorkStage] + ".Func=" + Convert.ToString(FuncType));
                        WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "-1");
                        Environment.Exit(Environment.ExitCode);
                        break;
                }

                //Go to next work. Check if arrive last work, close process.
                WorkStage++;
                //progressBar1.Value = WorkStage;
                myProgressBarValueUI(WorkStage, progressBar1);
                if (WorkStage == aWorkSeq.Count)
                {
                    iTemp = WebWorks_ini.getKeyValue("Configuration", "Work" + aWorkSeq[WorkStage-1] + ".Sleep", 0) + 1;
                    Thread.Sleep(iTemp);
                    PutErrorMessage("None");
                    WebWorks_ini.setKeyValue("ReturnedData", "StatusCode", "0");
                    Environment.Exit(Environment.ExitCode);
                }
                else
                {
                    bool PauseInSeq = Convert.ToBoolean(WebWorks_ini.getKeyValue("Configuration", "Work" + aWorkSeq[WorkStage-1] + ".Pause", 0));
                    if (PauseInSeq)
                    {
                        PutErrorMessage("Pause in Work" + aWorkSeq[WorkStage-1] + " now...");
                        while (true)
                        {
                            Thread.Sleep(1000);
                            PauseInSeq = Convert.ToBoolean(WebWorks_ini.getKeyValue("Configuration", "Work" + aWorkSeq[WorkStage-1] + ".Pause", 1));
                            if (!PauseInSeq)                     
                                break;                      
                        }
                    }
                    else
                    {
                        Thread.Sleep(SleepTime);
                    }
                }
            }
        }        

        private byte[] BuildMultipartPostData(string Boundary, string WorkX)
        {
            StringBuilder sb = new StringBuilder();

            for (int j = 1; j < BaseFieldNumber; j++)
            {
                string FieldInfo = WebWorks_ini.getKeyValue("Configuration", WorkX + ".Field" + Convert.ToString(j), "");
                if (FieldInfo == "")
                    break;
                else
                {
                    List<string> aFieldInfo = new List<string>(FieldInfo.Split(','));
                    for (int i = 0; i < aFieldInfo.Count; i++)
                        aFieldInfo[i] = aFieldInfo[i].Trim();

                    sb.AppendLine("--" + Boundary);
                    sb.AppendLine("Content-Disposition: form-data; name=\"" + aFieldInfo[0] + "\"");
                    sb.Append(Environment.NewLine);
                    sb.AppendLine(aFieldInfo[1]);
                }
            }
            //string FileNamefield, string FilePath, string FileType, string SubmitFieldName, string SubmitFieldValue           
            sb.AppendLine("--" + Boundary);

            string UploadInfo = WebWorks_ini.getKeyValue("Configuration", WorkX + ".Upload", "");
            List<string> aUploadInfo = new List<string>(UploadInfo.Split(','));
            for (int i = 0; i < aUploadInfo.Count; i++)
                aUploadInfo[i] = aUploadInfo[i].Trim();

            FileInfo myFileInfo = new FileInfo(Path.Combine(Application.StartupPath, aUploadInfo[1]));
            if (myFileInfo.Exists == false)
            {
                throw new FileNotFoundException("The file was not found.", Path.Combine(Application.StartupPath, aUploadInfo[1]));
            }
            sb.AppendLine(string.Format("Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"", aUploadInfo[0], myFileInfo.Name));
            sb.AppendLine(string.Format("Content-Type: {0}", aUploadInfo[2]));
            sb.Append(Environment.NewLine);


            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            try
            {
                bw.Write(Encoding.UTF8.GetBytes(sb.ToString()));
                FileStream Files = File.OpenRead(Path.Combine(Application.StartupPath, aUploadInfo[1]));
                byte[] buffer = new byte[1024];
                int bytesRead = 0;
                while ((bytesRead = Files.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (bytesRead == 1024)
                        bw.Write(buffer);
                    else
                    {
                        byte[] buffer2 = new byte[bytesRead];
                        for (int i = 0; i <= bytesRead - 1; i++)
                        {
                            buffer2[i] = buffer[i];
                        }
                        bw.Write(buffer2);
                    }
                }
                for (int j = 1; j < BaseFieldNumber; j++)
                {
                    string FieldInfo = WebWorks_ini.getKeyValue("Configuration", WorkX + ".DownField" + Convert.ToString(j), "");
                    if (FieldInfo == "")
                        break;
                    else
                    {
                        List<string> aFieldInfo = new List<string>(FieldInfo.Split(','));
                        for (int i = 0; i < aFieldInfo.Count; i++)
                            aFieldInfo[i] = aFieldInfo[i].Trim();

                        bw.Write(Encoding.UTF8.GetBytes(Environment.NewLine));
                        bw.Write(Encoding.UTF8.GetBytes("--" + Boundary + "\r\n"));
                        bw.Write(Encoding.UTF8.GetBytes("Content-Disposition: form-data; name=\"" + aFieldInfo[0] + "\"\r\n"));
                        bw.Write(Encoding.UTF8.GetBytes("\r\n" + aFieldInfo[1]));                       
                    }
                }
                bw.Write(Encoding.UTF8.GetBytes(Environment.NewLine));                               
                bw.Write(Encoding.UTF8.GetBytes("--" + Boundary + "--\r\n"));
                ms.Flush();
                ms.Position = 0;
                byte[] result = ms.ToArray();
                bw.Close();
                Files.Close();
                return result;
            }
            catch (Exception Ex)
            {
                PutErrorMessage(Ex);
                throw Ex;
            }
        }

        private string Load_URL(string WorkX, enumURL_Type Method)
        {            
            string sURL = WebWorks_ini.getKeyValue("Configuration", WorkX + ".URL", "");
            if (sURL == "")
            {
                return "";
            }
            sURL = sDestIP + sURL;
            switch (Method)
            {
                /*
                 * WorkX.URL=/XXX.cgi?AAA=1&BBB=2
                 * Find "?" and capture the real URL
                 * URL = "/XXX.cgi"
                 */
                case enumURL_Type.QUESTION_MARK:
                    int pos;
                    pos = sURL.IndexOf('?');
                    if (pos != -1)
                    {                       
                        sURL = sURL.Substring(0, pos);
                    }
            	    break;
                /*
                 * WorkX.URL=/XXX.cgi
                 * URL = "/XXX.cgi"
                 */
                case enumURL_Type.NORMAL:
                    break;
                default:
                    break;
            }
            return sURL;
        }

        private string Load_PostContent(string WorkX, enumPostContentType Method)
        {
            string PostContent = "";
            switch (Method)
            {
                /*
                 * WorkX.URL=/XXX.cgi?AAA=1&BBB=2
                 * Find "?" and capture the postcontent
                 * PostContent = "AAA=1&BBB=2"
                 */
                case enumPostContentType.QUESTION_MARK:
                    string sURL = WebWorks_ini.getKeyValue("Configuration", WorkX + ".URL", "");
                    int pos;                   
                    pos = sURL.IndexOf('?');
                    if (pos != -1)
                    {
                        PostContent = sURL.Substring(pos + 1, sURL.Length - (pos + 1));                        
                    }
                    break;
                /*
                 * WorkX.PostContent=AAA=1&BBB=2
                 * PostContent = "AAA=1&BBB=2"
                 */
                case enumPostContentType.POSTCONTENT:
                    PostContent = WebWorks_ini.getKeyValue("Configuration", WorkX + ".PostContent", "");
                    break;
                /*
                 * WorkX.PostContent1=AAA=1&
                 * WorkX.PostContent2=BBB=2
                 * Combine PostContent1 and PostContent2 to PostContent
                 * PostContent = "AAA=1&BBB=2"
                 */
                case enumPostContentType.POSTCONTENT_X:
                    StringBuilder PostContentBuilder = new StringBuilder();
                    for (int i = 1; i <= BaseFieldNumber; i++)
                    {
                        string PostContentAdd = WebWorks_ini.getKeyValue("Configuration", WorkX + ".PostContent" + Convert.ToString(i), "");
                        if (PostContentAdd != "")
                        {
                            PostContentBuilder.Append(PostContentAdd);
                        }
                        else
                        {
                            break;
                        }
                    }
                    PostContent = PostContentBuilder.ToString();
                    break;
                default:                    
                    break;
            }
            return PostContent;
        }

        private bool EditRevData_CompareKeyword(string WorkX, string SourceString)
        {
            //位移植, 若網頁中關鍵字不只一個時使用
            int iOffset = 0;
            //Example: iOffset == 0 -> 位移0次 -> 第1個關鍵字
            //         iOffset == 1 -> 位移1次 -> 第2個關鍵字

            //計算找尋關鍵字失敗的次數
            int iFailCount = 0;

            //輸出至WebWork.ini的結果: WorkX.Result=不合格項目的數量[, 不合格項目的編號 ..]
            string sResult = "";

            //從RegExp1做到RegExp+BaseFieldNumber, 若沒有則跳出for
            for (int i = 1; i <= BaseFieldNumber; i++)
            {
                string sRegExp = WebWorks_ini.getKeyValue("Configuration", WorkX + ".RegExp" + Convert.ToString(i), "");
                if (sRegExp == "")
                {
                    if (i <= 1)
                    {
                        PutErrorMessage("-> " + WorkX + ".RegExp=");
                        return false;
                    }
                    break;
                }
                //把WebWork.ini中RegExpX以逗點分隔至string List
                List<string> aRegExp = new List<string>(sRegExp.Split(','));
                for (int j = 0; j < aRegExp.Count; j++)
                    aRegExp[j] = aRegExp[j].Trim();
                if (aRegExp[1] == "" || aRegExp[2] == "")
                {
                    PutErrorMessage("-> " + WorkX + ".RegExp= param2 and param3 can't be empty");
                    return false;
                }
                //設定位移植
                if (aRegExp.Count == 4)
                    iOffset = Convert.ToInt32(aRegExp[3]);
                else
                    iOffset = 0;
                //尋找關鍵字1的位置
                int KeyWordPos1 = SourceString.IndexOf(aRegExp[0]);
                //找不到?
                if (KeyWordPos1 == -1)
                {
                    iFailCount = iFailCount + 1;
                    sResult = sResult + ", " + Convert.ToString(i);
                }
                //找到嚕
                else
                {
                    int KeyWordPos1_temp = 0;
                    while (iOffset > 0)
                    {
                        //找下一個KeyWord
                        KeyWordPos1_temp = SourceString.IndexOf(aRegExp[0], KeyWordPos1 + aRegExp[0].Length);
                        if (KeyWordPos1_temp != -1)
                            KeyWordPos1 = KeyWordPos1_temp;
                        //then
                        iOffset = iOffset - 1;
                    }
                    //MessageBox.Show(Convert.ToString(KeyWordPos1));
                    //從關鍵字1的位置找關鍵字2
                    int KeyWordPos2 = SourceString.IndexOf(aRegExp[2], KeyWordPos1 + aRegExp[0].Length);
                    //找不到關鍵字2或超過設定的距離
                    if (KeyWordPos2 == -1 || KeyWordPos2 - (KeyWordPos1 + aRegExp[0].Length) > Convert.ToInt32(aRegExp[1]))
                    {
                        iFailCount = iFailCount + 1;
                        sResult = sResult + ", " + Convert.ToString(i);
                    }
                }
            }
            //輸出至WebWork.ini
            sResult = Convert.ToString(iFailCount) + sResult;
            WebWorks_ini.setKeyValue("ReturnedData", WorkX + ".Result", sResult);
            return true;
        }

        private bool EditRevData_GetStringAfterKeyword(string WorkX, string SourceString)
        {
            int iOffset = 0;
            for (int i = 1; i <= BaseFieldNumber; i++)
            {
                string sRegExp = WebWorks_ini.getKeyValue("Configuration", WorkX + ".RegExp" + Convert.ToString(i), "");
                if (sRegExp == "")
                {
                    if (i <= 1)
                    {
                        PutErrorMessage("-> " + WorkX + ".RegExp=");
                        return false;
                    }
                    break;
                }
                List<string> aRegExp = new List<string>(sRegExp.Split(','));
                for (int j = 0; j < aRegExp.Count; j++)
                    aRegExp[j] = aRegExp[j].Trim();
                if (aRegExp[1] == "" || aRegExp[2] == "")
                {
                    PutErrorMessage("-> " + WorkX + ".RegExp= param2 and param3 can't be empty");
                    return false;
                }
                if (aRegExp.Count == 4)
                    iOffset = Convert.ToInt32(aRegExp[3]);
                else
                    iOffset = 0;
                int KeyWordPos1 = SourceString.IndexOf(aRegExp[0]);
                if (KeyWordPos1 == -1)
                {
                    PutErrorMessage("-> " + WorkX + ".RegExp= Can't find RegExp" + Convert.ToString(i) + " keyword in Web");
                    return false;
                }
                else
                {
                    int KeyWordPos1_temp = 0;
                    while (iOffset > 0)
                    {
                        //do something...
                        KeyWordPos1_temp = SourceString.IndexOf(aRegExp[0], KeyWordPos1 + aRegExp[0].Length);
                        if (KeyWordPos1_temp != -1)
                            KeyWordPos1 = KeyWordPos1_temp;
                        //then
                        iOffset = iOffset - 1;
                    }
                    int CaptureStringLength;
                    if (KeyWordPos1 + aRegExp[0].Length + Convert.ToInt32(aRegExp[1]) + Convert.ToInt32(aRegExp[2]) > SourceString.Length)
                    {
                        int SourceStringAndCaptureStringLengthDiff = KeyWordPos1 + aRegExp[0].Length + Convert.ToInt32(aRegExp[1]) + Convert.ToInt32(aRegExp[2]) - SourceString.Length;
                        CaptureStringLength = Convert.ToInt32(aRegExp[2]) - SourceStringAndCaptureStringLengthDiff;
                    }
                    else
                    {
                        CaptureStringLength = Convert.ToInt32(aRegExp[2]);
                    }
                    string SubStr = SourceString.Substring(KeyWordPos1 + aRegExp[0].Length + Convert.ToInt32(aRegExp[1]), CaptureStringLength);
                    SubStr = SubStr.Trim();
                    SubStr = SubStr.Replace("\r\n", "");
                    SubStr = SubStr.Replace("\n", "");
                    WebWorks_ini.setKeyValue("ReturnedData", WorkX + ".SubStr" + Convert.ToString(i), SubStr);
                }
            }
            return true;
        }

        private void EditRequestData(string WorkX, string sURL, enumHttpMethod Method, string PostContent, out HttpWebRequest req)
        {
            //Used for write POST content
            byte[] bs;
            //Used for POST multi part (Upload File)            
            string boundary = "---------------------------7dc39010340a0e";            
            req = (HttpWebRequest)HttpWebRequest.Create(sURL);
            switch (Method)
            {
                case enumHttpMethod.POST:
                    req.Method = "POST";                
                    req.ServicePoint.Expect100Continue = false;
                    req.AllowAutoRedirect = false;
            	    break;
                case enumHttpMethod.POST_MULTIPART:
                    req.Method = "POST";
                    req.ContentType = "multipart/form-data; boundary=" + boundary;
                    req.ServicePoint.Expect100Continue = false;
                    req.AllowWriteStreamBuffering = false;   
                    break;
                case enumHttpMethod.GET:
                    req.Method = "GET";
                    break;
                default:
                    MessageBox.Show("Should not be this");
                    break;
            }
           
            if (InProcessAuth)
                req.Headers.Add(Authorization); 
            req.Timeout = WebWorks_ini.getKeyValue("Configuration", WorkX + ".Timeout", 100000);

            //是否使用儲存的Cookie
            /*
            string isLogin = WebWorks_ini.getKeyValue("Configuration", WorkX + ".Msg", "");
            if (!isLogin.Contains("Login to DUT UI") && AutoCookie)
            {
                req.CookieContainer = new CookieContainer();
                req.CookieContainer = myCookie;
            }*/
            
            int LoadCookie = WebWorks_ini.getKeyValue("Configuration", WorkX + ".LoadCookie", 0);
            if (LoadCookie == 1)
            {
                req.CookieContainer = new CookieContainer();               
                req.CookieContainer = ReadCookiesFromDisk("CookiesTemp");                
            }

            //---H---Modify http header and add extra header------------------------------
            string content_type = WebWorks_ini.getKeyValue("Configuration", WorkX + ".ContentType", "");
            if (Method != enumHttpMethod.POST_MULTIPART)
            {
                if (content_type != "")
                    req.ContentType = content_type;
                else
                    req.ContentType = "application/x-www-form-urlencoded";
            }            
            string accept = WebWorks_ini.getKeyValue("Configuration", WorkX + ".Accept", "");
            if (accept != "")
                req.Accept = accept;
            string referer = WebWorks_ini.getKeyValue("Configuration", WorkX + ".Referer", "");
            if (referer != "")
                req.Referer = referer;
            string user_agent = WebWorks_ini.getKeyValue("Configuration", WorkX + ".User-Agent", "");
            if (user_agent != "")
                req.UserAgent = user_agent;
            for (int i = 1; i <= BaseFieldNumber; i++)
            {
                string headeradd = WebWorks_ini.getKeyValue("Configuration", WorkX + ".HeaderAdd" + Convert.ToString(i), "");
                if (headeradd != "")
                {
                    req.Headers.Add(headeradd);
                }
            }
            //---E---Modify http header and add extra header------------------------------
            if (Method == enumHttpMethod.POST)
            {
                bs = Encoding.ASCII.GetBytes(PostContent);
                req.ContentLength = bs.Length;
                using (Stream reqStream = req.GetRequestStream())
                {
                    reqStream.Write(bs, 0, bs.Length);
                }
            }
            else if (Method == enumHttpMethod.POST_MULTIPART)
            {
                byte[] multipartPostData = BuildMultipartPostData(boundary, WorkX);
                req.ContentLength = multipartPostData.Length;
                BinaryWriter bw = new BinaryWriter(req.GetRequestStream());
                bw.Write(multipartPostData);
                bw.Flush();
                bw.Close();
            }
        }
        
        private bool SendHttpReqAndGetResp(string WorkX, enumHttpFunction FuncType, enumHttpMethod Method)
        {
            int ResponseCode;
            string sURL = "";
            string PostContent = "";            

            switch (FuncType)
            {
                case enumHttpFunction.PostData:
                    sURL = Load_URL(WorkX, enumURL_Type.QUESTION_MARK);
                    PostContent = Load_PostContent(WorkX, enumPostContentType.QUESTION_MARK);
                    break;                
                case enumHttpFunction.PostData_MultiPart:
                case enumHttpFunction.StrSearch:
                case enumHttpFunction.GetString:
                case enumHttpFunction.GetData:
                    sURL = Load_URL(WorkX, enumURL_Type.NORMAL);                   
                    break;
                case enumHttpFunction.PostData2:
                    sURL = Load_URL(WorkX, enumURL_Type.NORMAL);
                    PostContent = Load_PostContent(WorkX, enumPostContentType.POSTCONTENT);
                    break;
                case enumHttpFunction.PostData3:
                case enumHttpFunction.StrSearch_POST:
                case enumHttpFunction.GetString_POST:
                    sURL = Load_URL(WorkX, enumURL_Type.NORMAL);
                    PostContent = Load_PostContent(WorkX, enumPostContentType.POSTCONTENT_X);
                    break;                
                default:
                    MessageBox.Show("Should not be this");
                    break;
            }            
            if (sURL == "")
            {
                PutErrorMessage("-> " + WorkX + ".URL=");
                return false;
            }                        

            try
            {
                HttpWebRequest req;
                EditRequestData(WorkX, sURL, Method, PostContent, out req);
                HttpWebResponse wr = (HttpWebResponse)req.GetResponse();

                //檢查是否儲存Cookie                
                int SaveCookie = WebWorks_ini.getKeyValue("Configuration", WorkX + ".SaveCookie", 0);
                if (SaveCookie == 1)
                {
                    req.CookieContainer = new CookieContainer();
                    string CookieAdd = WebWorks_ini.getKeyValue("Configuration", "CookieAdd", "");
                    if (CookieAdd != "")
                    {
                        List<string> ListCookieAdd = new List<string>(CookieAdd.Split(';'));
                        for (int i = 0; i < ListCookieAdd.Count; i++)
                        {
                            ListCookieAdd[i] = ListCookieAdd[i].Trim();
                            List<string> ListNameAndContent = new List<string>(ListCookieAdd[i].Split('='));
                            req.CookieContainer.Add(new Uri(sDestIP), new Cookie(ListNameAndContent[0], ListNameAndContent[1]));
                        }
                    }                    
                    WriteCookiesToDisk("CookiesTemp", req.CookieContainer);
                }

                ResponseCode = (int)wr.StatusCode;
                WebWorks_ini.setKeyValue("ReturnedData", WorkX + ".ResponseCode", ResponseCode);

                //---H---use response data to do something------------------------------   
                int WebEncoding = WebWorks_ini.getKeyValue("Configuration", "Encoding", 0);           
                StreamReader sr;
                if (wr.GetResponseHeader("Transfer-Encoding") == "chunked")
                {
                    if (WebEncoding == 0)                    
                        sr = new StreamReader(ReadChunked(wr));
                    else
                        sr = new StreamReader(ReadChunked(wr), Encoding.GetEncoding(WebEncoding));
                }
                else
                {                    
                    if (WebEncoding == 0)
                        sr = new StreamReader(wr.GetResponseStream());
                    else
                        sr = new StreamReader(wr.GetResponseStream(), Encoding.GetEncoding(WebEncoding));
                }
                string ResponseData = sr.ReadToEnd();
                //MessageBox.Show(ResponseData);

                switch (FuncType)
                {                   
                    case enumHttpFunction.StrSearch:
                    case enumHttpFunction.StrSearch_POST:
                        if (!EditRevData_CompareKeyword(WorkX, ResponseData))
                        {
                            return false;
                        }
                        break;
                    case enumHttpFunction.GetString:
                    case enumHttpFunction.GetString_POST:
                        if (!EditRevData_GetStringAfterKeyword(WorkX, ResponseData))
                        {
                            return false;
                        }
                        break;
                    default:
                        break;
                }                
                wr.Close();
                //---E---use response data to do something------------------------------ 
            }
            catch (WebException we)
            {
                PutErrorMessage(we);
                if (WebWorks_ini.getKeyValue("Configuration", WorkX + ".IgnoreResponse", 0) == 1)
                {
                    int SaveCookie = WebWorks_ini.getKeyValue("Configuration", WorkX + ".SaveCookie", 0);
                    if (SaveCookie == 1)
                    {
                        CookieContainer CookieContainerTemp = new CookieContainer();
                        CookieContainerTemp.Add(((HttpWebResponse)we.Response).Cookies);
                        string CookieAdd = WebWorks_ini.getKeyValue("Configuration", "CookieAdd", "");
                        if (CookieAdd != "")
                        {
                            List<string> ListCookieAdd = new List<string>(CookieAdd.Split(';'));
                            for (int i = 0; i < ListCookieAdd.Count; i++)
                            {                                
                                ListCookieAdd[i] = ListCookieAdd[i].Trim();
                                List<string> ListNameAndContent = new List<string>(ListCookieAdd[i].Split('='));
                                CookieContainerTemp.Add(new Uri(sDestIP), new Cookie(ListNameAndContent[0], ListNameAndContent[1]));
                            }
                        }
                        WriteCookiesToDisk("CookiesTemp", CookieContainerTemp);
                    }
                    return true;
                }
                if (we.Status == WebExceptionStatus.ProtocolError)
                {
                    ResponseCode = (int)((HttpWebResponse)we.Response).StatusCode;
                    WebWorks_ini.setKeyValue("ReturnedData", WorkX + ".ResponseCode", ResponseCode);
                }
                return false;
            }
            catch (Exception UnknowEx)
            {
                PutErrorMessage(UnknowEx);
                return false;
            }
            return true;
        }

        private void PutErrorMessage(Exception Ex)
        {           
            string EnterDeleteEx = Ex.Message.Replace(Environment.NewLine, " ");
            WebWorks_ini.setKeyValue("ErrorMessage", "Err", EnterDeleteEx);
        }

        private void PutErrorMessage(string Ex)
        {
            WebWorks_ini.setKeyValue("ErrorMessage", "Err", Ex);
        }

        public bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) 
        {
            // Always accept
            return true; 
        }

        public static void WriteCookiesToDisk(string file, CookieContainer cookieJar)
        {
            using (Stream stream = File.Create(file))
            {
                try
                {
                    //Console.Out.Write("Writing cookies to disk... ");
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(stream, cookieJar);
                    //Console.Out.WriteLine("Done.");
                }
                catch (Exception e)
                {
                    //Console.Out.WriteLine("Problem writing cookies to disk: " + e.GetType());
                    throw new ArgumentException("Problem writing cookies to disk: " + e.GetType());
                }
            }
        }

        public static CookieContainer ReadCookiesFromDisk(string file)
        {
            try
            {
                using (Stream stream = File.Open(file, FileMode.Open))
                {
                    //Console.Out.Write("Reading cookies from disk... ");
                    BinaryFormatter formatter = new BinaryFormatter();
                    //Console.Out.WriteLine("Done.");
                    return (CookieContainer)formatter.Deserialize(stream);
                }
            }
            catch (Exception e)
            {
                //Console.Out.WriteLine("Problem reading cookies from disk: " + e.GetType());
                throw new ArgumentException("Problem reading cookies from disk: " + e.GetType());
            }
        }

        public static MemoryStream ReadChunked(HttpWebResponse res)
        {　　
            /*如果服务器使用了Transfer-Encoding：chunked缓冲输出，则只要服务器端Flush了，
             * 就会触发此方法，而不是等到服务器发送过来的内容全部发送完才触发，而且与是不是异步HttpWebRequest请求也没有关系。
             * 相反，如果服务器没有使用Transfer-Encoding：chunked缓冲输出，则不管是异步HttpWebRequest请求还是同步HttpWebRequest请求，
             * 都得等到服务器发送过来的内容全部发送完才触发此方法。
             */
            Stream stream = res.GetResponseStream();
            int length = 0;
            if (res.ContentLength > 0)
            {
                length = (int)res.ContentLength;
            }
            else
            {
                length = 3000;
            }
            MemoryStream memory = new MemoryStream(length);            
            int count = 0;
            //每次从服务器返回流中读取5000个字节
            byte[] buffer = new byte[5000];      
            while (true)
            {
                /*如果服务器使用了Transfer-Encoding：chunked缓冲输出，
                 * 则如果已经读取了服务器第一次Flush的内容后服务器第二次Flush的内容还没有接收到，则会阻塞当前线程，
                 * 直到接收到服务器第二次Flush的内容（第三，四。。。次Flush也是一样），所以很可能会造成读取一次返回的count不满5000，
                 * 但下一次继续读取返回的count却不是0的情况
                 */

                count = stream.Read(buffer, 0, buffer.Length);
                if (count == 0)
                {
                    break;
                }
                memory.Write(buffer, 0, count);                
            }                 
            stream.Close();
            //将流的可读位置设置到起始值
            memory.Seek(0, SeekOrigin.Begin);
            return memory;
        }
    }
}
