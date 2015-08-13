using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace RedCell.Net
{
    /// <summary>
    /// Fetches web pages.
    /// </summary>
    public class Fetch
    {
        #region Constants
        private const int DefaultDefaultRetries = 5;
        private const int DefaultDefaultTimeout = 60000;
        private const int DefaultDefaultRetrySleep = 10000;
        #endregion

        #region Enums
        private enum Verbs { Get, Post }
        #endregion

        #region Initialiation
        /// <summary>
        /// Initializes static members of the <see cref="Fetch"/> class.
        /// </summary>
        static Fetch()
        {
            DefaultRetries = DefaultDefaultRetries;
            DefaultTimeout = DefaultDefaultTimeout;
            DefaultRetrySleep = DefaultDefaultRetrySleep;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Fetch" /> class.
        /// </summary>
        /// <param name="url">The URL.</param>
        public Fetch(string url)
        {
            Url = url;
            Headers = new WebHeaderCollection();
            Retries = DefaultRetries;
            Timeout = DefaultTimeout;
            RetrySleep = DefaultRetrySleep;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the URL.
        /// </summary>
        /// <value>The URL.</value>
        public string Url { get; private set; }

        /// <summary>
        /// Gets the headers.
        /// </summary>
        /// <value>The headers.</value>
        public WebHeaderCollection Headers { get; private set; }

        /// <summary>
        /// Gets the response.
        /// </summary>
        public HttpWebResponse Response { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public NetworkCredential Credential { get; set; }

        /// <summary>
        /// Gets the response data.
        /// </summary>
        public byte[] ResponseData { get; private set; }

        /// <summary>
        /// Gets or sets the retries.
        /// </summary>
        /// <value>The retries.</value>
        public int Retries { get; set; }

        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        /// <value>The timeout.</value>
        public int Timeout { get; set; }

        /// <summary>
        /// Gets or sets the retry sleep in milliseconds.
        /// </summary>
        /// <value>The retry sleep.</value>
        public int RetrySleep { get; set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Fetch"/> is success.
        /// </summary>
        /// <value><c>true</c> if success; otherwise, <c>false</c>.</value>
        public bool Success { get; private set; }

        /// <summary>
        /// Gets or sets the default retries.
        /// </summary>
        /// <value>The default retries.</value>
        public static int DefaultRetries { get; set; }

        /// <summary>
        /// Gets or sets the default timeout.
        /// </summary>
        /// <value>The default timeout.</value>
        public static int DefaultTimeout { get; set; }

        /// <summary>
        /// Gets or sets the default retry sleep.
        /// </summary>
        /// <value>The default retry sleep.</value>
        public static int DefaultRetrySleep { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Makes an HTTP GET request.
        /// </summary>
        public void Get()
        {
            Load(Verbs.Get);
        }

        /// <summary>
        /// Makes an HTTP POST request.
        /// </summary>
        /// <param name="data">The data.</param>
        public void Post(Dictionary<string, string> data)
        {
            Load(Verbs.Post, data);
        }

        /// <summary>
        /// Performs the trnasaction.
        /// </summary>
        private void Load(Verbs verb, Dictionary<string, string> data = null)
        {
            for (int retry = 0; retry < Retries; retry++)
            {
                try
                {
                    var req = HttpWebRequest.Create(Url) as HttpWebRequest;

                    // Behaviour
                    req.AllowAutoRedirect = true;
                    ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;

                    // Headers
                    if (Credential != null)
                        req.Credentials = Credential;
                    req.Headers = Headers;
                    req.Timeout = Timeout;
                    req.Method = verb == Verbs.Post ? "POST" : "GET";

                    // Post
                    if (verb == Verbs.Post)
                    {
                        req.ContentType = "application/x-www-form-urlencoded";
                        if (data != null)
                        {
                            string urlEncoded = "";
                            foreach (var pair in data)
                                urlEncoded = pair.Key + "=" + Uri.EscapeDataString(pair.Value) + "&";
                            byte[] encoded = Encoding.UTF8.GetBytes(urlEncoded);
                            var os = req.GetRequestStream();
                            os.Write(encoded, 0, encoded.Length);
                        }
                    }

                    // Request
                    Response = req.GetResponse() as HttpWebResponse;
                    switch (Response.StatusCode)
                    {
                        case HttpStatusCode.Found:
                            // This is a redirect.
                            Console.WriteLine("Found (302), ignoring ");
                            break;

                        case HttpStatusCode.OK:
                            // This is a valid page.
                            using (var sr = Response.GetResponseStream())
                            using (var ms = new MemoryStream())
                            {
                                for (int b; (b = sr.ReadByte()) != -1; )
                                    ms.WriteByte((byte)b);
                                ResponseData = ms.ToArray();
                            }
                            break;

                        default:
                            // This is unexpected.
                            Console.WriteLine(Response.StatusCode);
                            break;
                    }
                    Success = true;
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine("Exception " + ex.Message);
                    Response = ex.Response as HttpWebResponse;
                    if (ex.Status == WebExceptionStatus.Timeout)
                    {
                        Thread.Sleep(RetrySleep);
                        continue;
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Gets the response from an HTTP GET to the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>The response</returns>
        public static byte[] Get(string url)
        {
            var f = new Fetch(url);
            f.Get();
            return f.ResponseData;
        }

        /// <summary>
        /// Gets the response from an HTTP GET to the specified URL.
        /// </summary>
        /// <typeparam name="T">The desired return type.</typeparam>
        /// <param name="url">The URL.</param>
        /// <returns>The response</returns>
        /// <exception cref="System.InvalidCastException">Cannot return the requested type:  + ex.Message</exception>
        public static T Get<T>(string url)
        {
            var f = new Fetch(url);
            f.Get();
            return f.ConvertResponse<T>();
        }

        /// <summary>
        /// Gets the response from an HTTP GET to the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="data">The data.</param>
        /// <returns>The response</returns>
        public static byte[] Post(string url, Dictionary<string, string> data = null)
        {
            var f = new Fetch(url);
            f.Post(data);
            return f.ResponseData;
        }

        /// <summary>
        /// Gets the response from an HTTP GET to the specified URL.
        /// </summary>
        /// <typeparam name="T">The desired return type.</typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="data">The data.</param>
        /// <returns>The response</returns>
        /// <exception cref="System.InvalidCastException">Cannot return the requested type:  + ex.Message</exception>
        public static T Post<T>(string url, Dictionary<string, string> data)
        {
            var f = new Fetch(url);
            f.Post(data);
            return f.ConvertResponse<T>();
        }

        public T ConvertResponse<T>()
        {
            if (typeof(T) == typeof(byte[]))
                return (T)(object)ResponseData;
            if (typeof(T) == typeof(string))
                return (T)(object)GetString();

            // Attempt conversion of IConvertible.
            try { return (T)Convert.ChangeType(GetString(), typeof(T)); }
            catch (Exception ex) { throw new InvalidCastException("Cannot return the requested type: " + ex.Message); }
        }

        /// <summary>
        /// Gets the string.
        /// </summary>
        /// <returns></returns>
        private string GetString()
        {
            var encoder = string.IsNullOrEmpty(Response.ContentEncoding) ? Encoding.UTF8 : Encoding.GetEncoding(Response.ContentEncoding);
            if (ResponseData == null)
                return string.Empty;
            return encoder.GetString(ResponseData);
        }
        #endregion
    }
}
