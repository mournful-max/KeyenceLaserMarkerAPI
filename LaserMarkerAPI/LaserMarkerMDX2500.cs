using System;

namespace LaserMarkerAPI
{
    public class LaserMarkerMDX2500 : LaserMarkerBase
    {
        public string CurrentProgramNo
        {
            private set; get;
        }

        public LaserMarkerMDX2500()
        {
            CurrentProgramNo = "<empty>";
        }

        public Response Run(string command, int receiveTimeout)
        {
            int previousReceiveTimeout = -1;

            if (this.Connected)
            {
                try
                {
                    previousReceiveTimeout = _Socket.ReceiveTimeout;
                    _Socket.ReceiveTimeout = receiveTimeout;
                }
                catch { }
            }
            Response response = base.Run(command);

            if (this.Connected)
            {
                try
                {
                    _Socket.ReceiveTimeout = previousReceiveTimeout;
                }
                catch { }
            }
            return response;
        }

        public Response IsReady() => Run(CommandReadPrefix + CommandSeparator + CommandReady + CommandTerminator);

        public Response ErrorClear() => Run(CommandWritePrefix + CommandSeparator + CommandErrorClear + CommandTerminator);

        public Response ErrorStatus() => Run(CommandReadPrefix + CommandSeparator + CommandError + CommandTerminator);

        public Response StartMarking(int? receiveTimeout = null)
        {
            if (receiveTimeout != null)
            {
                return Run(CommandWritePrefix + CommandSeparator + CommandStartMarking + CommandTerminator, receiveTimeout.Value);
            }
            else
            {
                return Run(CommandWritePrefix + CommandSeparator + CommandStartMarking + CommandTerminator);
            }
        }

        public Response ChangeCharacterStrings(uint[] blks, string[] strings)
        {
            if (blks.Length != strings.Length || blks.Length == 0)
            {
                throw new ArgumentException(CommandBLK + " count: " + blks.Length + ", but " + CommandCharacterString + " count: " + strings.Length);
            }
            string wxLinkedCommands = CommandWritePrefix;

            for (int i = 0; i < blks.Length; ++i)
            {
                wxLinkedCommands += CommandSeparator + CommandBLK             + CommandAssignment + blks[i].ToString()
                                  + CommandSeparator + CommandCharacterString + CommandAssignment + strings[i];
            }
            wxLinkedCommands += CommandTerminator;

            return Run(wxLinkedCommands);
        }

        public Response ChangeProgram(string programNo)
        {
            Response response = Run(CommandWritePrefix + CommandSeparator + CommandProgramNo + CommandAssignment + programNo + CommandTerminator);

            if (response.Result)
            {
                CurrentProgramNo = programNo;
            }
            return response;
        }

        public const string CommandBLK             = "BLK";
        public const string CommandReady           = "Ready";
        public const string CommandError           = "Error";
        public const string CommandProgramNo       = "ProgramNo";
        public const string CommandAssignment      = "=";
        public const string CommandErrorClear      = "ErrorClear";
        public const string CommandStopMarking     = "StopMarking";
        public const string CommandStartMarking    = "StartMarking";
        public const string CommandCharacterString = "CharacterString";
    }
}
