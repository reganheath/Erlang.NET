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
     * Provides a carrier for Erlang messages.
     * 
     * Instances of this class are created to package header and payload information
     * in received Erlang messages so that the recipient can obtain both parts with
     * a single call to {@link OtpMbox#receiveMsg receiveMsg()}.
     * 
     * The header information that is available is as follows:
     * - a tag indicating the type of message
     * - the intended recipient of the message, either as a
     * {@link OtpErlangPid pid} or as a string, but never both.
     * - (sometimes) the sender of the message. Due to some eccentric
     * characteristics of the Erlang distribution protocol, not all messages have
     * information about the sending process. In particular, only messages whose tag
     * is {@link OtpMsg#regSendTag regSendTag} contain sender information.
     * 
     * Message are sent using the Erlang external format (see separate
     * documentation). When a message is received and delivered to the recipient
     * {@link OtpMbox mailbox}, the body of the message is still in this external
     * representation until {@link #getMsg getMsg()} is called, at which point the
     * message is decoded. A copy of the decoded message is stored in the OtpMsg so
     * that subsequent calls to {@link #getMsg getMsg()} do not require that the
     * message be decoded a second time.
     */
    public class OtpMsg
    {
        public const int linkTag = 1;
        public const int sendTag = 2;
        public const int exitTag = 3;
        public const int unlinkTag = 4;
        public const int regSendTag = 6;
        public const int groupLeaderTag = 7; // Not handled
        public const int exit2Tag = 8;

        protected IOtpErlangObject payload;

        // send has receiver pid but no sender information
        internal OtpMsg(OtpErlangPid to, OtpInputStream paybuf)
        {
            Type = sendTag;
            ToPid = to;
            Stream = paybuf;
        }

        // send has receiver pid but no sender information
        internal OtpMsg(OtpErlangPid to, IOtpErlangObject payload)
        {
            Type = sendTag;
            ToPid = to;
            this.payload = payload;
        }

        // send_reg has sender pid and receiver name
        internal OtpMsg(OtpErlangPid from, string toName, OtpInputStream paybuf)
        {
            Type = regSendTag;
            FromPid = from;
            ToName = toName;
            Stream = paybuf;
        }

        // send_reg has sender pid and receiver name
        internal OtpMsg(OtpErlangPid from, string toName, IOtpErlangObject payload)
        {
            Type = regSendTag;
            FromPid = from;
            ToName = toName;
            this.payload = payload;
        }

        // exit (etc) has from, to, reason
        internal OtpMsg(int tag, OtpErlangPid from, OtpErlangPid to, IOtpErlangObject reason)
        {
            Type = tag;
            FromPid = from;
            ToPid = to;
            payload = reason;
        }

        // special case when reason is an atom (i.e. most of the time)
        internal OtpMsg(int tag, OtpErlangPid from, OtpErlangPid to, string reason)
        {
            Type = tag;
            FromPid = from;
            ToPid = to;
            payload = new OtpErlangAtom(reason);
        }

        // other message types (link, unlink)
        internal OtpMsg(int tag, OtpErlangPid from, OtpErlangPid to)
        {
            // convert TT-tags to equiv non-TT versions
            Type = (tag > 10 ? tag - 10 : tag);
            FromPid = from;
            ToPid = to;
        }

        /**
         * Get the type marker from this message. The type marker identifies the
         * type of message. Valid values are the ``tag'' constants defined in this
         * class.
         * 
         * The tab identifies not only the type of message but also the content of
         * the OtpMsg object, since different messages have different components, as
         * follows:
         * 
         * sendTag identifies a "normal" message. The recipient is a
         * {@link OtpErlangPid Pid} and it is available through {@link
         * #getRecipientPid getRecipientPid()}. Sender information is not available.
         * The message body can be retrieved with {@link #getMsg getMsg()}.
         * 
         * regSendTag also identifies a "normal" message. The recipient here is
         * a string and it is available through {@link #getRecipientName
         * getRecipientName()}. Sender information is available through
         * #getSenderPid getSenderPid()}. The message body can be retrieved with
         * {@link #getMsg getMsg()}.
         * 
         * linkTag identifies a link request. The Pid of the sender is
         * available, as well as the Pid to which the link should be made.
         * 
         * exitTag and exit2Tag messages are sent as a result of broken links.
         * Both sender and recipient Pids and are available through the
         * corresponding methods, and the "reason" is available through
         * {@link #getMsg getMsg()}.
         */
        public int Type { get; protected set; }

        /**
         * Get the Pid of the sender of this message.
         * 
         * For messages sent to names, the Pid of the sender is included with the
         * message. The sender Pid is also available for link, unlink and exit
         * messages. It is not available for sendTag messages sent to Pids.
         */
        public OtpErlangPid FromPid { get; protected set; }

        /**
         * Get the name of the recipient for this message.
         * 
         * Messages are sent to Pids or names. If this message was sent to a name
         * then the name is returned by this method.
         */
        public string ToName { get; protected set; }

        /**
         * Get the Pid of the recipient for this message, if it is a sendTag
         * message.
         * 
         * Messages are sent to Pids or names. If this message was sent to a Pid
         * then the Pid is returned by this method. The recipient Pid is also
         * available for link, unlink and exit messages.
         */
        public OtpErlangPid ToPid { get; protected set; }

        /**
         * Get the payload from this message without deserializing it.
         */
        internal OtpInputStream Stream { get; set; }

        /**
         * Deserialize and return a new copy of the message contained in this
         * OtpMsg.
         * 
         * The first time this method is called the actual payload is deserialized
         * and the Erlang term is created. Calling this method subsequent times will
         * not cuase the message to be deserialized additional times, instead the
         * same Erlang term object will be returned.
         */
        public IOtpErlangObject Payload
        {
            get
            {
                if (payload == null)
                    payload = Stream.ReadAny();
                return payload;
            }
        }

        /**
         * Get the name of the recipient for this message, if it is a regSendTag
         * message.
         * 
         * Messages are sent to Pids or names. If this message was sent to a name
         * then the name is returned by this method.
         */
        public object To
        {
            get => (ToName != null ? (object)ToName : (object)ToPid);
        }

        public override string ToString()
        {
            return $"MSG {Type} from {FromPid} to {ToPid} ({ToName}) containing {Payload}";
        }
    }
}

