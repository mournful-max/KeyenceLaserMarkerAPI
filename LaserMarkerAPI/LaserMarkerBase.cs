using System;
using System.Net;
using System.Text;
using System.Reflection;
using System.Net.Sockets;

namespace LaserMarkerAPI
{
    public class LaserMarkerBase
    {
        protected Socket _Socket;

        // May throw an exception
        protected virtual Socket GetNewConfiguredSocket(int sendTimeout, int receiveTimeout, int sendBufferSize, int receiveBufferSize)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Don't allow another socket to bind to this port.
            socket.ExclusiveAddressUse = true;

            // The socket will linger for 1 second after Socket.Close is called.
            socket.LingerState = new LingerOption(true, 1);

            // Disable the Nagle Algorithm for this tcp socket.
            socket.NoDelay = true;

            // Set the Time To Live (TTL) to 2 router hops.
            socket.Ttl = 32;

            socket.SendTimeout       = sendTimeout;
            socket.ReceiveTimeout    = receiveTimeout;

            socket.ReceiveBufferSize = receiveBufferSize;
            socket.SendBufferSize    = sendBufferSize;

            return socket;
        }

        public virtual bool Connected => _Socket != null && _Socket.Connected;

        public virtual void Connect(string ip, ushort port = 50002, int sendTimeout = 3000, int receiveTimeout = 10000, int sendBufferSize = 8192, int receiveBufferSize = 8192, int connectionTimeout = 60000)
        {
            string thisMethodFullName = this.GetType() + "." + MethodBase.GetCurrentMethod().Name;

            if (this.Connected)
            {
                throw new Exception(thisMethodFullName + ": operation impossible as it was already performed yet.");
            }
            _Socket = GetNewConfiguredSocket(sendTimeout, receiveTimeout, sendBufferSize, receiveBufferSize);
            //_Socket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));

            IAsyncResult result  = _Socket.BeginConnect(new IPEndPoint(IPAddress.Parse(ip), port), null, null);
            bool         success = result.AsyncWaitHandle.WaitOne(connectionTimeout, true);

            if (_Socket.Connected)
            {
                _Socket.EndConnect(result);
            }
            else
            {
                _Socket.Close();
                throw new Exception(thisMethodFullName + "No response within " + connectionTimeout.ToString() + "ms from " + ip + ':' + port.ToString());
            }
        }

        public virtual void Disconnect()
        {
            if (this.Connected)
            {
                try
                {
                    _Socket.Close();
                }
                catch
                {
                }
                _Socket = null;
            }
        }

        public virtual Response Run(string command)
        {
            if (!this.Connected)
            {
                return new Response(false, String.Empty, new Exception("No connection with Laser Marker has been established."));
            }
            if (!command.StartsWith(CommandWritePrefix)
             && !command.StartsWith(CommandReadPrefix))
            {
                return new Response(false, "A command must starts with \"" + CommandWritePrefix + "\" or \"" + CommandReadPrefix + "\" prefix.");
            }
            if (!command.EndsWith(CommandTerminator))
            {
                return new Response(false, "A command must ends with \"" + CommandTerminator + "\" terminator.");
            }
            Response responseWrapper = new Response();
            try
            {
                byte[] msg = Encoding.ASCII.GetBytes(command);
                int bytesSentTotal = 0;
                do
                {
                    bytesSentTotal += _Socket.Send(msg, bytesSentTotal, msg.Length - bytesSentTotal, SocketFlags.None);
                }
                while (bytesSentTotal < msg.Length);

                byte[] response = new byte[_Socket.ReceiveBufferSize];
                int bytesReceived = 0;
                do
                {
                    bytesReceived = _Socket.Receive(response);
                    responseWrapper.Message += Encoding.UTF8.GetString(response, 0, bytesReceived);
                }
                while (_Socket.Available > 0);
            }
            catch (Exception exception)
            {
                return new Response(false, String.Empty, exception);
            }
            NormalizeResponse(ref responseWrapper);

            return responseWrapper;
        }

        private void NormalizeResponse(ref Response response)
        {
            if (response == null)
            {
                response = new Response(false, String.Empty, new NullReferenceException("An attempt to normalize the NUL."));
                return;
            }
            string responseOkSuffix = CommandSeparator + ResponseOkStatus;

            if (response.Message.StartsWith(CommandWritePrefix + responseOkSuffix)
             || response.Message.StartsWith(CommandReadPrefix  + responseOkSuffix))
            {
                response.Result = true;
            }
            else
            {
                response.Result = false;
            }
        }

        ~LaserMarkerBase()
        {
            Disconnect();
        }

        public sealed class Response
        {
            public bool      Result    { internal set; get; }
            public string    Message   { internal set; get; }
            public Exception Exception { internal set; get; }

            internal Response()
            {
                Result    = false;
                Message   = String.Empty;
                Exception = null;
            }

            internal Response(bool result, string message, Exception exception = null)
            {
                Result    = result;
                Message   = message ?? String.Empty;
                Exception = exception;
            }

            public override string ToString()
            {
                string result           = Result ? "success" : "failure";
                string body             = "Response result: " + result + ". Response message: " + Message.Replace(Environment.NewLine, " ") + ".";
                string exceptionMessage = String.Empty;

                if (Exception != null && !String.IsNullOrWhiteSpace(Exception.Message))
                {
                    exceptionMessage += "Exception: " + Exception.Message.Replace(Environment.NewLine, " ") + ".";

                    if (Exception.InnerException != null && !String.IsNullOrWhiteSpace(Exception.InnerException.Message))
                    {
                        exceptionMessage += " Inner exception: " + Exception.InnerException.Message.Replace(Environment.NewLine, " ") + ".";
                    }
                }
                return exceptionMessage.Length == 0 ? body : body + ' ' + exceptionMessage;
            }

            public static bool operator ==(Response obj1, Response obj2)
            {
                if (ReferenceEquals(obj1, obj2))
                {
                    return true;
                }
                if (ReferenceEquals(obj1, null))
                {
                    return false;
                }
                return obj1.Equals(obj2);
            }

            public static bool operator !=(Response obj1, Response obj2) => !(obj1 == obj2);

            public bool Equals(Response other)
            {
                if (ReferenceEquals(other, null))
                {
                    return false;
                }
                if (ReferenceEquals(this, other))
                {
                    return true;
                }
                string thisExceptionToString  = String.Empty;
                string otherExceptionToString = String.Empty;

                if (this.Exception != null && !String.IsNullOrWhiteSpace(this.Exception.Message))
                {
                    thisExceptionToString += "Exception: " + this.Exception.Message.Replace(Environment.NewLine, " ") + ".";

                    if (this.Exception.InnerException != null && !String.IsNullOrWhiteSpace(this.Exception.InnerException.Message))
                    {
                        thisExceptionToString += " Inner exception: " + this.Exception.InnerException.Message.Replace(Environment.NewLine, " ") + ".";
                    }
                }
                if (other.Exception != null && !String.IsNullOrWhiteSpace(other.Exception.Message))
                {
                    otherExceptionToString += "Exception: " + other.Exception.Message.Replace(Environment.NewLine, " ") + ".";

                    if (other.Exception.InnerException != null && !String.IsNullOrWhiteSpace(other.Exception.InnerException.Message))
                    {
                        otherExceptionToString += " Inner exception: " + other.Exception.InnerException.Message.Replace(Environment.NewLine, " ") + ".";
                    }
                }
                return this.Result && other.Result && this.Message == other.Message && thisExceptionToString == otherExceptionToString;
            }

            public override bool Equals(object obj) => Equals(obj as Response);

            public override int GetHashCode()
            {
                string exceptionMessage = String.Empty;

                if (Exception != null && !String.IsNullOrWhiteSpace(Exception.Message))
                {
                    exceptionMessage += "Exception: " + Exception.Message.Replace(Environment.NewLine, " ") + ".";

                    if (Exception.InnerException != null && !String.IsNullOrWhiteSpace(Exception.InnerException.Message))
                    {
                        exceptionMessage += " Inner exception: " + Exception.InnerException.Message.Replace(Environment.NewLine, " ") + ".";
                    }
                }
                int hashCode;
                unchecked
                {
                    hashCode = (Message.GetHashCode() * 397) ^ Result.GetHashCode();
                    return (hashCode * 397) ^ exceptionMessage.GetHashCode();
                }
            }
        }

        public const string CommandWritePrefix = "WX";
        public const string CommandReadPrefix  = "RX";
        public const string CommandSeparator   = ",";
        public const string CommandTerminator  = "\r";
        public const string ResponseOkStatus   = "OK";
    }
}
