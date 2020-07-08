/*
 * Copyright 2020 Regan Heath
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
namespace Erlang.NET
{
    /**
     * Exception raised when a communication channel is broken. This can be caused
     * for a number of reasons, for example:
     * 
     * <ul>
     * <li> an error in communication has occurred
     * <li> a remote process has sent an exit signal
     * <li> a linked process has exited
     * </ul>
     * 
     * @see OtpConnection
     */
    public class OtpExit : OtpException
    {
        /**
         * Get the reason associated with this exit signal.
         */
        public IOtpErlangObject Reason { get; } = null;

        /**
         * Get the pid that sent this exit.
         */
        public OtpErlangPid Pid { get; } = null;

        /**
         * Create an OtpErlangExit exception with the given reason.
         */
        public OtpExit(IOtpErlangObject reason) : base(reason.ToString()) => Reason = reason;

        /**
         * Equivalent to <code>OtpErlangExit(new OtpErlangAtom(reason)</code>.
         */
        public OtpExit(string reason) : this(new OtpErlangAtom(reason)) { }

        /**
         * Create an OtpErlangExit exception with the given reason and sender pid.
         */
        public OtpExit(IOtpErlangObject reason, OtpErlangPid pid)
            : base(reason.ToString())
        {
            Reason = reason;
            Pid = pid;
        }

        /**
         * Equivalent to <code>OtpErlangExit(new OtpErlangAtom(reason), pid)</code>.
         */
        public OtpExit(string reason, OtpErlangPid pid) : this(new OtpErlangAtom(reason), pid) { }
    }
}
