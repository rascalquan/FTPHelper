using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MyNamespace
{
    #region 文件信息结构

    public struct FileStruct
    {
        public string Flags;
        public string Owner;
        public string Group;
        public bool IsDirectory;
        public DateTime CreateTime;
        public string Name;
    }

    public enum FileListStyle
    {
        UnixStyle,
        WindowsStyle,
        Unknown
    }

    #endregion

    public class FTPHelper
    {
        #region 属性信息

        /// <summary>
        /// FTP请求对象
        /// </summary>
        FtpWebRequest Request = null;

        /// <summary>
        /// FTP响应对象
        /// </summary>
        FtpWebResponse Response = null;

        /// <summary>
        /// FTP服务器地址
        /// </summary>
        private Uri _Uri;

        /// <summary>
        /// FTP服务器地址
        /// </summary>
        public Uri Uri
        {
            get
            {
                if (_DirectoryPath == "/")
                {
                    return _Uri;
                }
                else
                {
                    string strUri = _Uri.ToString();
                    if (strUri.EndsWith("/"))
                    {
                        strUri = strUri.Substring(0, strUri.Length - 1);
                    }
                    return new Uri(strUri + this.DirectoryPath);
                }
            }
            set
            {
                if (value.Scheme != Uri.UriSchemeFtp)
                {
                    throw new Exception("Ftp 地址格式错误!");
                }
                _Uri = new Uri(value.GetLeftPart(UriPartial.Authority));
                _DirectoryPath = value.AbsolutePath;
                if (!_DirectoryPath.EndsWith("/"))
                {
                    _DirectoryPath += "/";
                }
            }
        }

        /// <summary>
        /// 当前工作目录
        /// </summary>
        private string _DirectoryPath;

        /// <summary>
        /// 当前工作目录
        /// </summary>
        public string DirectoryPath
        {
            get { return _DirectoryPath; }
            set { _DirectoryPath = value; }
        }

        /// <summary>
        /// FTP登录用户
        /// </summary>
        private string _UserName;

        /// <summary>
        /// FTP登录用户
        /// </summary>
        public string UserName
        {
            get { return _UserName; }
            set { _UserName = value; }
        }

        /// <summary>
        /// 错误信息
        /// </summary>
        private string _ErrorMsg;
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMsg
        {
            get { return _ErrorMsg; }
            set { _ErrorMsg = value; }
        }

        /// <summary>
        /// FTP登录密码
        /// </summary>
        private string _Password;

        /// <summary>
        /// FTP登录密码
        /// </summary>
        public string Password
        {
            get { return _Password; }
            set { _Password = value; }
        }

        /// <summary>
        /// 连接FTP服务器的代理服务
        /// </summary>
        private WebProxy _Proxy = null;

        /// <summary>
        /// 连接FTP服务器的代理服务
        /// </summary>
        public WebProxy Proxy
        {
            get
            {
                return _Proxy;
            }
            set
            {
                _Proxy = value;
            }
        }

        /// <summary>
        /// 是否需要删除临时文件
        /// </summary>
        private bool _isDeleteTempFile = false;

        /// <summary>
        /// 异步上传所临时生成的文件
        /// </summary>
        private string _UploadTempFile = "";
        #endregion

        #region 构造析构函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="FtpUri">FTP地址</param>
        /// <param name="strUserName">登录用户名</param>
        /// <param name="strPassword">登录密码</param>
        public FTPHelper(Uri FtpUri, string strUserName, string strPassword)
        {
            this._Uri = new Uri(FtpUri.GetLeftPart(UriPartial.Authority));
            //this._Uri = FtpUri;
            _DirectoryPath = FtpUri.AbsolutePath;
            if (!_DirectoryPath.EndsWith("/"))
            {
                _DirectoryPath += "/";
            }
            this._UserName = strUserName;
            this._Password = strPassword;
            this._Proxy = null;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="FtpUri">FTP地址</param>
        /// <param name="strUserName">登录用户名</param>
        /// <param name="strPassword">登录密码</param>
        /// <param name="objProxy">连接代理</param>
        public FTPHelper(Uri FtpUri, string strUserName, string strPassword, WebProxy objProxy)
        {
            this._Uri = new Uri(FtpUri.GetLeftPart(UriPartial.Authority));
            _DirectoryPath = FtpUri.AbsolutePath;
            if (!_DirectoryPath.EndsWith("/"))
            {
                _DirectoryPath += "/";
            }
            this._UserName = strUserName;
            this._Password = strPassword;
            this._Proxy = objProxy;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public FTPHelper()
        {
            this._UserName = "anonymous";  //匿名用户
            this._Password = "@anonymous";
            this._Uri = null;
            this._Proxy = null;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~FTPHelper()
        {
            if (Response != null)
            {
                Response.Close();
                Response = null;
            }
            if (Request != null)
            {
                Request.Abort();
                Request = null;
            }
        }

        #endregion

        #region 建立连接

        public bool CheckFTPConnection(Uri uri)
        {
            try
            {
                // Create a web request for an invalid site.
                FtpWebRequest Request = (FtpWebRequest)WebRequest.Create(uri);

                Request.Credentials = new NetworkCredential(this.UserName, this.Password);
                Request.Method = WebRequestMethods.Ftp.ListDirectory;
                // Get the associated response for the above request.
                FtpWebResponse myFtpWebResponse = (FtpWebResponse)Request.GetResponse();
                if (myFtpWebResponse.StatusCode == FtpStatusCode.ConnectionClosed)
                {
                    return false;
                }

                myFtpWebResponse.Close();
                return true;
            }
            catch (Exception e)
            {
                ErrorMsg = e.ToString();
                return false;
            }
        }

        /// <summary>
        /// 建立FTP链接,返回响应对象
        /// </summary>
        /// <param name="uri">FTP地址</param>
        /// <param name="FtpMethod">操作命令</param>
        private FtpWebResponse Open(Uri uri, string FtpMethod)
        {
            try
            {
                Request = (FtpWebRequest)WebRequest.Create(uri);
                Request.Method = FtpMethod;
                Request.UseBinary = true;
                Request.Credentials = new NetworkCredential(this.UserName, this.Password);
                if (this.Proxy != null)
                {
                    Request.Proxy = this.Proxy;
                }
                return (FtpWebResponse)Request.GetResponse();
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }

        /// <summary>
        /// 建立FTP链接,返回请求对象
        /// </summary>
        /// <param name="uri">FTP地址</param>
        /// <param name="FtpMethod">操作命令</param>
        private FtpWebRequest OpenRequest(Uri uri, string FtpMethod)
        {
            try
            {
                Request = (FtpWebRequest)WebRequest.Create(uri);
                Request.Method = FtpMethod;
                Request.UseBinary = true;
                Request.Credentials = new NetworkCredential(this.UserName, this.Password);
                if (this.Proxy != null)
                {
                    Request.Proxy = this.Proxy;
                }
                return Request;
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }

        #endregion

        #region 下载文件

        /// <summary>
        /// 创建人：  Sam Zhao
        /// 创建日期：2013-03-18
        /// 描述：    下载FTP目录下所有文件
        /// </summary>
        /// <param name="RemoteFileName"></param>
        /// <param name="LocalPath"></param>
        /// <returns></returns>
        public bool DownloadFileAll(string LocalPath)
        {
            FileStruct[] list = ListFilesAndDirectories();
            foreach (FileStruct file in list)
            {
                DownloadFile(file.Name, LocalPath, true, false);
            }

            return true;
        }

        /// <summary>
        /// 从FTP服务器下载文件，使用与远程文件同名的文件名来保存文件
        /// </summary>
        /// <param name="RemoteFileName">远程文件名</param>
        /// <param name="LocalPath">本地路径</param>
        public bool DownloadFile(string RemoteFileName, string LocalPath, bool checkIfRemoteFileExisted)
        {
            return DownloadFile(RemoteFileName, LocalPath, RemoteFileName, checkIfRemoteFileExisted);
        }

        /// <summary>
        /// 创建人：  Sam Zhao
        /// 创建日期：2013-03-19
        /// 描述：    下载FTP文件
        /// </summary>
        /// <param name="RemoteFileName"></param>
        /// <param name="LocalPath"></param>
        /// <param name="overwriteLocalFile"></param>
        /// <param name="checkIfRemoteFileExisted">检查远程FTP文件是否存在</param>
        /// <returns></returns>
        public bool DownloadFile(string RemoteFileName, string LocalPath, bool overwriteLocalFile, bool checkIfRemoteFileExisted)
        {
            return DownloadFile(RemoteFileName, LocalPath, RemoteFileName, overwriteLocalFile, checkIfRemoteFileExisted);
        }

        /// <summary>
        /// 从FTP服务器下载文件，指定本地路径和本地文件名
        /// </summary>
        /// <param name="RemoteFileName">远程文件名</param>
        /// <param name="LocalPath">本地路径</param>
        /// <param name="LocalFilePath">保存文件的本地路径,后面带有"\"</param>
        /// <param name="LocalFileName">保存本地的文件名</param>
        public bool DownloadFile(string RemoteFileName, string LocalPath, string LocalFileName, bool overwriteLocalFile, bool checkIfRemoteFileExisted)
        {
            byte[] bt = null;
            try
            {
                if (!IsValidFileChars(RemoteFileName) || !IsValidFileChars(LocalFileName) || !IsValidPathChars(LocalPath))
                {
                    throw new Exception("非法文件名或目录名!");
                }
                if (!Directory.Exists(LocalPath))
                {
                    throw new Exception("本地文件路径不存在!");
                }

                if (checkIfRemoteFileExisted)
                {
                    if (!FileExist(RemoteFileName))
                    {
                        throw new Exception("远程文件路径不存在!");
                    }
                }

                string LocalFullPath = Path.Combine(LocalPath, LocalFileName);
                if (!overwriteLocalFile && File.Exists(LocalFullPath))
                {
                    throw new Exception("当前路径下已经存在同名文件！");
                }
                bt = DownloadFile(RemoteFileName);

                FileStream stream = new FileStream(LocalFullPath, FileMode.Create);

                if (bt != null)
                {
                    stream.Write(bt, 0, bt.Length);
                }
                stream.Flush();
                stream.Close();
                return true;

            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }

        public bool DownloadFile(string RemoteFileName, string LocalPath, string LocalFileName, bool checkIfRemoteFileExisted)
        {
            return DownloadFile(RemoteFileName, LocalPath, LocalFileName, false, checkIfRemoteFileExisted);
        }

        /// <summary>
        /// 从FTP服务器下载文件，返回文件二进制数据
        /// </summary>
        /// <param name="RemoteFileName">远程文件名</param>
        public byte[] DownloadFile(string RemoteFileName)
        {
            try
            {
                if (!IsValidFileChars(RemoteFileName))
                {
                    throw new Exception("非法文件名或目录名!");
                }
                Response = Open(new Uri(this.Uri.ToString() + RemoteFileName), WebRequestMethods.Ftp.DownloadFile);
                Stream Reader = Response.GetResponseStream();

                MemoryStream mem = new MemoryStream(1024 * 500);
                byte[] buffer = new byte[1024];
                int bytesRead = 0;
                int TotalByteRead = 0;
                while (true)
                {
                    bytesRead = Reader.Read(buffer, 0, buffer.Length);
                    TotalByteRead += bytesRead;
                    if (bytesRead == 0)
                        break;
                    mem.Write(buffer, 0, bytesRead);
                }
                if (mem.Length > 0)
                {
                    return mem.ToArray();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }

        #endregion

        #region 上传文件
        /// <summary>
        /// 上传文件到FTP服务器
        /// </summary>
        /// <param name="LocalFullPath">本地带有完整路径的文件名</param>
        public bool UploadFile(string LocalFullPath)
        {
            return UploadFile(LocalFullPath, Path.GetFileName(LocalFullPath), false);
        }

        /// <summary>
        /// 上传文件到FTP服务器
        /// </summary>
        /// <param name="LocalFullPath">本地带有完整路径的文件</param>
        /// <param name="OverWriteRemoteFile">是否覆盖远程服务器上面同名的文件</param>
        public bool UploadFile(string LocalFullPath, bool OverWriteRemoteFile)
        {
            return UploadFile(LocalFullPath, Path.GetFileName(LocalFullPath), OverWriteRemoteFile);
        }

        /// <summary>
        /// 上传文件到FTP服务器
        /// </summary>
        /// <param name="LocalFullPath">本地带有完整路径的文件</param>
        /// <param name="RemoteFileName">要在FTP服务器上面保存文件名</param>
        public bool UploadFile(string LocalFullPath, string RemoteFileName)
        {
            return UploadFile(LocalFullPath, RemoteFileName, false);
        }

        /// <summary>
        /// 上传文件到FTP服务器
        /// </summary>
        /// <param name="LocalFullPath">本地带有完整路径的文件名</param>
        /// <param name="RemoteFileName">要在FTP服务器上面保存文件名</param>
        /// <param name="OverWriteRemoteFile">是否覆盖远程服务器上面同名的文件</param>
        public bool UploadFile(string LocalFullPath, string RemoteFileName, bool OverWriteRemoteFile)
        {
            try
            {
                if (!IsValidFileChars(RemoteFileName) || !IsValidFileChars(Path.GetFileName(LocalFullPath)) || !IsValidPathChars(Path.GetDirectoryName(LocalFullPath)))
                {
                    throw new Exception("非法文件名或目錄名!");
                }
                if (File.Exists(LocalFullPath))
                {
                    FileStream Stream = new FileStream(LocalFullPath, FileMode.Open, FileAccess.Read);
                    byte[] bt = new byte[Stream.Length];
                    Stream.Read(bt, 0, (Int32)Stream.Length);   //注意，因为Int32的最大限制，最大上传文件只能是大约2G多一点
                    Stream.Close();
                    return UploadFile(bt, RemoteFileName, OverWriteRemoteFile);
                }
                else
                {
                    throw new Exception("本地文件不存在!");
                }
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }

        /// <summary>
        /// 上传文件到FTP服务器
        /// </summary>
        /// <param name="FileBytes">上传的二进制数据</param>
        /// <param name="RemoteFileName">要在FTP服务器上面保存文件名</param>
        public bool UploadFile(byte[] FileBytes, string RemoteFileName)
        {
            if (!IsValidFileChars(RemoteFileName))
            {
                throw new Exception("非法文件名或目录名!");
            }
            return UploadFile(FileBytes, RemoteFileName, false);
        }

        /// <summary>
        /// 上传文件到FTP服务器
        /// </summary>
        /// <param name="FileBytes">文件二进制内容</param>
        /// <param name="RemoteFileName">要在FTP服务器上面保存文件名</param>
        /// <param name="OverWriteRemoteFile">是否覆盖远程服务器上面同名的文件</param>
        public bool UploadFile(byte[] FileBytes, string RemoteFileName, bool OverWriteRemoteFile)
        {
            try
            {
                if (!IsValidFileChars(RemoteFileName))
                {
                    throw new Exception("非法文件名");
                }
                if (!OverWriteRemoteFile && FileExist(RemoteFileName))
                {
                    throw new Exception("FTP服務上面已經存在同名文件！");
                }
                Response = Open(new Uri(this.Uri.ToString() + RemoteFileName), WebRequestMethods.Ftp.UploadFile);
                Stream requestStream = Request.GetRequestStream();
                MemoryStream mem = new MemoryStream(FileBytes);

                byte[] buffer = new byte[1024];
                int bytesRead = 0;
                int TotalRead = 0;
                while (true)
                {
                    bytesRead = mem.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;
                    TotalRead += bytesRead;
                    requestStream.Write(buffer, 0, bytesRead);
                }
                requestStream.Close();
                Response = (FtpWebResponse)Request.GetResponse();
                mem.Close();
                mem.Dispose();
                FileBytes = null;
                return true;
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }
        #endregion        

        #region 删除文件
        /// <summary>
        /// 从FTP服务器上面删除一个文件
        /// </summary>
        /// <param name="RemoteFileName">远程文件名</param>
        public void DeleteFile(string RemoteFileName)
        {
            DeleteFile(RemoteFileName, true);
        }

        /// <summary>
        /// 从FTP服务器上面删除一个文件
        /// </summary>
        /// <param name="RemoteFileName">远程文件名</param>
        /// <param name="checkRemotingFileValid">是否检查远程文件名为非法</param>
        public void DeleteFile(string RemoteFileName, bool checkRemotingFileValid)
        {
            try
            {
                if (checkRemotingFileValid)
                {
                    if (!IsValidFileChars(RemoteFileName))
                    {
                        throw new Exception("文件名非法！");
                    }
                }

                Response = Open(new Uri(this.Uri.ToString() + RemoteFileName), WebRequestMethods.Ftp.DeleteFile);
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }
        #endregion 

        #region 列出目录文件信息
        /// <summary>
        /// 列出FTP服务器上面当前目录的所有文件和目录
        /// </summary>
        public FileStruct[] ListFilesAndDirectories()
        {
            Response = Open(this.Uri, WebRequestMethods.Ftp.ListDirectoryDetails);
            //StreamReader stream = new StreamReader(Response.GetResponseStream(), Encoding.Default);
            StreamReader stream = new StreamReader(Response.GetResponseStream(), Encoding.UTF8);
            string Datastring = stream.ReadToEnd();
            FileStruct[] list = GetList(Datastring);
            return list;
        }

        /// <summary>
        /// 列出FTP服务器上面当前目录的所有文件和目录
        /// </summary>
        public FileStruct[] ListFilesAndDirectories(int subStrLength, string nameSplit)
        {
            Response = Open(this.Uri, WebRequestMethods.Ftp.ListDirectoryDetails);
            //StreamReader stream = new StreamReader(Response.GetResponseStream(), Encoding.Default);
            StreamReader stream = new StreamReader(Response.GetResponseStream(), Encoding.UTF8);
            string Datastring = stream.ReadToEnd();
            FileStruct[] list = GetList(Datastring, subStrLength, nameSplit);
            return list;
        }

        /// <summary>
        /// 列出FTP服务器上面当前目录的所有文件
        /// </summary>
        public FileStruct[] ListFiles(string searchPattern)
        {
            FileStruct[] listAll = ListFilesAndDirectories();
            List<FileStruct> listFile = new List<FileStruct>();
            bool doFilter = false;
            Regex reg = null;

            if (!string.IsNullOrEmpty(searchPattern))
            {
                doFilter = true;

                string searchString = Regex.Escape(searchPattern);
                searchString = searchString.Replace(@"\?", ".").Replace(@"\*", @".*");//.Replace(@"\?", ".")
                //searchString = string.Format("^{0}$", searchPattern);
                reg = new Regex(searchString);
            }
            foreach (FileStruct file in listAll)
            {
                if (!file.IsDirectory)
                {
                    if (doFilter)
                    {
                        if (reg.IsMatch(file.Name))
                        {
                            listFile.Add(file);
                        }
                        continue;
                    }
                    listFile.Add(file);
                }
            }
            return listFile.ToArray();
        }

        public FileStruct[] ListFiles()
        {
            return ListFiles(null);
        }

        /// <summary>
        /// 列出FTP服务器上面当前目录的所有的目录
        /// </summary>
        public FileStruct[] ListDirectories()
        {
            FileStruct[] listAll = ListFilesAndDirectories();
            List<FileStruct> listDirectory = new List<FileStruct>();
            foreach (FileStruct file in listAll)
            {
                if (file.IsDirectory)
                {
                    listDirectory.Add(file);
                }
            }
            return listDirectory.ToArray();
        }

        /// <summary>
        /// 获得文件和目录列表
        /// </summary>
        /// <param name="datastring">FTP返回的列表字符信息</param>
        private FileStruct[] GetList(string datastring)
        {
            List<FileStruct> myListArray = new List<FileStruct>();
            string[] dataRecords = datastring.Split('\n');
            FileListStyle _directoryListStyle = GuessFileListStyle(dataRecords);
            foreach (string s in dataRecords)
            {
                if (_directoryListStyle != FileListStyle.Unknown && s != "")
                {
                    FileStruct f = new FileStruct();
                    f.Name = "..";
                    switch (_directoryListStyle)
                    {
                        case FileListStyle.UnixStyle:
                            f = ParseFileStructFromUnixStyleRecord(s);
                            break;
                        case FileListStyle.WindowsStyle:
                            f = ParseFileStructFromWindowsStyleRecord(s);
                            break;
                    }
                    if (!(f.Name == "." || f.Name == ".."))
                    {
                        myListArray.Add(f);
                    }
                }
            }
            return myListArray.ToArray();
        }

        /// <summary>
        /// 获得文件和目录列表
        /// </summary>
        /// <param name="datastring">FTP返回的列表字符信息</param>
        private FileStruct[] GetList(string datastring, int subLength, string nameSplit)
        {
            List<FileStruct> myListArray = new List<FileStruct>();
            string[] dataRecords = datastring.Split('\n');
            FileListStyle _directoryListStyle = GuessFileListStyle(dataRecords);
            foreach (string s in dataRecords)
            {
                if (_directoryListStyle != FileListStyle.Unknown && s != "")
                {
                    FileStruct f = new FileStruct();
                    f.Name = "..";
                    switch (_directoryListStyle)
                    {
                        case FileListStyle.UnixStyle:
                            f = ParseFileStructFromUnixStyleRecord(s);
                            break;
                        case FileListStyle.WindowsStyle:
                            f = ParseFileStructFromWindowsStyleRecord(s);
                            break;
                    }
                    if (!(f.Name == "." || f.Name == ".."))
                    {
                        if (f.Name.Length > subLength)
                        {
                            if (string.Compare(f.Name.Substring(0, subLength), nameSplit, true) == 0)
                            {
                                myListArray.Add(f);
                            }
                        }
                    }
                }
            }
            return myListArray.ToArray();
        }

        /// <summary>
        /// 从Windows格式中返回文件信息
        /// </summary>
        /// <param name="Record">文件信息</param>
        private FileStruct ParseFileStructFromWindowsStyleRecord(string Record)
        {
            FileStruct f = new FileStruct();
            string processstr = Record.Trim();
            string dateStr = processstr.Substring(0, 8);
            processstr = (processstr.Substring(8, processstr.Length - 8)).Trim();
            string timeStr = processstr.Substring(0, 7);
            processstr = (processstr.Substring(7, processstr.Length - 7)).Trim();
            DateTimeFormatInfo myDTFI = new CultureInfo("en-US", false).DateTimeFormat;
            myDTFI.ShortTimePattern = "t";
            f.CreateTime = DateTime.Parse(dateStr + " " + timeStr, myDTFI);
            if (processstr.Substring(0, 5) == "<DIR>")
            {
                f.IsDirectory = true;
                processstr = (processstr.Substring(5, processstr.Length - 5)).Trim();
            }
            else
            {
                string[] strs = processstr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);   // true);
                processstr = strs[1];
                f.IsDirectory = false;
            }
            f.Name = processstr;
            return f;
        }

        /// <summary>
        /// 判断文件列表的方式Window方式还是Unix方式
        /// </summary>
        /// <param name="recordList">文件信息列表</param>
        private FileListStyle GuessFileListStyle(string[] recordList)
        {
            foreach (string s in recordList)
            {
                if (s.Length > 10
                 && Regex.IsMatch(s.Substring(0, 10), "(-|d)(-|r)(-|w)(-|x)(-|r)(-|w)(-|x)(-|r)(-|w)(-|x)"))
                {
                    return FileListStyle.UnixStyle;
                }
                else if (s.Length > 8
                 && Regex.IsMatch(s.Substring(0, 8), "[0-9][0-9]-[0-9][0-9]-[0-9][0-9]"))
                {
                    return FileListStyle.WindowsStyle;
                }
            }
            return FileListStyle.Unknown;
        }

        /// <summary>
        /// 从Unix格式中返回文件信息
        /// </summary>
        /// <param name="Record">文件信息</param>
        private FileStruct ParseFileStructFromUnixStyleRecord(string Record)
        {
            FileStruct f = new FileStruct();
            string processstr = Record.Trim();
            f.Flags = processstr.Substring(0, 10);
            f.IsDirectory = (f.Flags[0] == 'd');
            processstr = (processstr.Substring(11)).Trim();
            _cutSubstringFromStringWithTrim(ref processstr, ' ', 0);   //跳过一部分
            f.Owner = _cutSubstringFromStringWithTrim(ref processstr, ' ', 0);
            f.Group = _cutSubstringFromStringWithTrim(ref processstr, ' ', 0);
            _cutSubstringFromStringWithTrim(ref processstr, ' ', 0);   //跳过一部分
            string yearOrTime = processstr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[2];
            if (yearOrTime.IndexOf(":") >= 0)  //time
            {
                processstr = processstr.Replace(yearOrTime, DateTime.Now.Year.ToString());
            }
            f.CreateTime = DateTime.Parse(_cutSubstringFromStringWithTrim(ref processstr, ' ', 8));
            f.Name = processstr;   //最后就是名称
            return f;
        }

        /// <summary>
        /// 按照一定的规则进行字符串截取
        /// </summary>
        /// <param name="s">截取的字符串</param>
        /// <param name="c">查找的字符</param>
        /// <param name="startIndex">查找的位置</param>
        private string _cutSubstringFromStringWithTrim(ref string s, char c, int startIndex)
        {
            int pos1 = s.IndexOf(c, startIndex);
            string retString = s.Substring(0, pos1);
            s = (s.Substring(pos1)).Trim();
            return retString;
        }

        #endregion

        #region 目录或文件存在的判断

        /// <summary>
        /// 判断当前目录下指定的子目录是否存在
        /// </summary>
        /// <param name="RemoteDirectoryName">指定的目录名</param>
        public bool DirectoryExist(string RemoteDirectoryName)
        {
            try
            {
                if (!IsValidPathChars(RemoteDirectoryName))
                {
                    throw new Exception("目录名非法！");
                }
                FileStruct[] listDir = ListDirectories();
                foreach (FileStruct dir in listDir)
                {
                    if (dir.Name == RemoteDirectoryName)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }

        /// <summary>
        /// 判断一个远程文件是否存在服务器当前目录下面
        /// </summary>
        /// <param name="RemoteFileName">远程文件名</param>
        public bool FileExist(string RemoteFileName)
        {
            try
            {
                if (!IsValidFileChars(RemoteFileName))
                {
                    throw new Exception("文件名非法！");
                }
                FileStruct[] listFile = ListFiles();
                foreach (FileStruct file in listFile)
                {
                    if (file.Name == RemoteFileName)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }

        #endregion

        #region 文件、目录名称有效性判断

        /// <summary>
        /// 判断目录名中字符是否合法
        /// </summary>
        /// <param name="DirectoryName">目录名称</param>
        public bool IsValidPathChars(string DirectoryName)
        {
            char[] invalidPathChars = Path.GetInvalidPathChars();
            char[] DirChar = DirectoryName.ToCharArray();
            foreach (char C in DirChar)
            {
                if (Array.BinarySearch(invalidPathChars, C) >= 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 判断文件名中字符是否合法
        /// </summary>
        /// <param name="FileName">文件名称</param>
        public bool IsValidFileChars(string FileName)
        {
            char[] invalidFileChars = Path.GetInvalidFileNameChars();
            char[] NameChar = FileName.ToCharArray();
            foreach (char C in NameChar)
            {
                if (Array.BinarySearch(invalidFileChars, C) >= 0)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region 建立、删除子目录
        /// <summary>
        /// 在FTP服务器上当前工作目录建立一个子目录
        /// </summary>
        /// <param name="DirectoryName">子目录名称</param>
        public bool MakeDirectory(string DirectoryName)
        {
            try
            {
                if (!IsValidPathChars(DirectoryName))
                {
                    throw new Exception("目錄名稱非法！");
                }

                if (!DirectoryExist(DirectoryName))
                {
                    Response = Open(new Uri(this.Uri.ToString() + DirectoryName), WebRequestMethods.Ftp.MakeDirectory);
                }

                return true;
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }

        public bool MakeDirectory2(string DirectoryName)
        {
            try
            {
                string uri = this.Uri.ToString() + DirectoryName;
                FtpWebRequest refFtp = Connect(uri);
                refFtp.Method = WebRequestMethods.Ftp.MakeDirectory;
                FtpWebResponse response = (FtpWebResponse)refFtp.GetResponse();
                response.Close();
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }

            return true;
        }

        private FtpWebRequest Connect(string path)
        {
            FtpWebRequest refFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(path));
            refFtp.UseBinary = true;
            refFtp.Credentials = new NetworkCredential(this.UserName, this.Password);
            return refFtp;
        }

        /// <summary>
        /// 从当前工作目录中删除一个子目录
        /// </summary>
        /// <param name="DirectoryName">子目录名称</param>
        public bool RemoveDirectory(string DirectoryName)
        {
            try
            {
                if (!IsValidPathChars(DirectoryName))
                {
                    throw new Exception("目录名非法！");
                }
                if (!DirectoryExist(DirectoryName))
                {
                    throw new Exception("服务器上面不存在指定的文件名或目录名！");
                }
                Response = Open(new Uri(this.Uri.ToString() + DirectoryName), WebRequestMethods.Ftp.RemoveDirectory);
                return true;
            }
            catch (Exception ep)
            {
                ErrorMsg = ep.ToString();
                throw ep;
            }
        }
        #endregion
    }
}
