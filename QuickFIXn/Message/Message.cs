﻿using System;
using System.Text;
using QuickFix.Fields;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Generic;

namespace QuickFix
{
    public class Header : FieldMap
    {
        public int[] HEADER_FIELD_ORDER = { Tags.BeginString, Tags.BodyLength, Tags.MsgType };

        public Header()
            : base()
        { }

        public Header(Header src)
            : base(src)
        { }

        public override string CalculateString()
        {
            return base.CalculateString(new StringBuilder(), HEADER_FIELD_ORDER);
        }

        public override string CalculateString(StringBuilder sb, int[] preFields)
        {
            return base.CalculateString(sb, HEADER_FIELD_ORDER);
        }
    }

    public class Trailer : FieldMap
    {
        public int[] TRAILER_FIELD_ORDER = { Tags.SignatureLength, Tags.Signature, Tags.CheckSum };

        public Trailer()
            : base()
        { }

        public Trailer(Trailer src)
            : base(src)
        { }

        public override string CalculateString()
        {
            return base.CalculateString(new StringBuilder(), TRAILER_FIELD_ORDER);
        }

        public override string CalculateString(StringBuilder sb, int[] preFields)
        {
            return base.CalculateString(sb, TRAILER_FIELD_ORDER);
        }
    }

    /// <summary>
    /// Represents a FIX message
    /// </summary>
    public class Message : FieldMap
    {
        public const char SOH = '\u0001';
        private int field_;
        private bool validStructure_;

        #region Properties

        public Header Header { get; private set; }
        public Trailer Trailer { get; private set; }
        public DataDictionary.DataDictionary ApplicationDataDictionary { get; private set; }

        #endregion

        #region Constructors

        public Message()
        {
            this.Header = new Header();
            this.Trailer = new Trailer();
            this.validStructure_ = true;
        }

        public Message(string msgstr)
            : this(msgstr, true)
        { }

        public Message(string msgstr, bool validate)
            : this(msgstr, null, null, validate)
        {  }

        public Message(string msgstr, DataDictionary.DataDictionary dataDictionary, bool validate)
            : this()
        {
            this.ApplicationDataDictionary = dataDictionary;
            FromString(msgstr, validate, dataDictionary, dataDictionary, null);
        }

        public Message(string msgstr, DataDictionary.DataDictionary sessionDataDictionary, DataDictionary.DataDictionary appDD, bool validate)
            : this()
        {
            this.ApplicationDataDictionary = appDD;
            FromStringHeader(msgstr);
            if(IsAdmin())
                FromString(msgstr, validate, sessionDataDictionary, sessionDataDictionary, null);
            else
                FromString(msgstr, validate, sessionDataDictionary, appDD, null);
        }

        public Message(Message src)
            : base(src)
        {
            this.Header = new Header(src.Header);
            this.Trailer = new Trailer(src.Trailer);
            this.validStructure_ = src.validStructure_;
            this.field_= src.field_;
        }

        #endregion

        #region Static Methods

        public static bool IsAdminMsgType(string msgType)
        {
            return msgType.Length == 1 && "0A12345n".Contains(msgType[0]);
        }

        /// <summary>
        /// Parse the message type (tag 35) from a FIX string
        /// </summary>
        /// <param name="fixstring">the FIX string to parse</param>
        /// <returns>the message type as a MsgType object</returns>
        /// <exception cref="MessageParseError">if 35 tag is missing or malformed</exception>
        public static MsgType IdentifyType(string fixstring)
        {
            return new MsgType(GetMsgType(fixstring));
        }

        public static StringField ExtractField(string msgstr, ref int pos, DataDictionary.DataDictionary sessionDD, DataDictionary.DataDictionary appDD)
        {
            try
            {
                int tagend = msgstr.IndexOf('=', pos);
                int tag = Convert.ToInt32(msgstr.Substring(pos, tagend - pos));
                pos = tagend + 1;
                int fieldvalend = msgstr.IndexOf((char)1, pos);
                StringField field =  new StringField(tag, msgstr.Substring(pos, fieldvalend - pos));

                /*
                 TODO data dict stuff
                if (((null != sessionDD) && sessionDD.IsDataField(field.Tag)) || ((null != appDD) && appDD.IsDataField(field.Tag)))
                {
                    string fieldLength = "";
                    // Assume length field is 1 less
                    int lenField = field.Tag - 1;
                    // Special case for Signature which violates above assumption
                    if (Tags.Signature.Equals(field.Tag))
                        lenField = Tags.SignatureLength;

                    if ((null != group) && group.isSetField(lenField))
                    {
                        fieldLength = group.GetField(lenField);
                        soh = equalSign + 1 + atol(fieldLength.c_str());
                    }
                    else if (isSetField(lenField))
                    {
                        fieldLength = getField(lenField);
                        soh = equalSign + 1 + atol(fieldLength.c_str());
                    }
                }
                */

                pos = fieldvalend + 1;
                return field;
            }
            catch (System.ArgumentOutOfRangeException e)
            {
                throw new MessageParseError("Error at position (" + pos + ") while parsing msg (" + msgstr + ")", e);
            }
            catch (System.OverflowException e)
            {
                throw new MessageParseError("Error at position (" + pos + ") while parsing msg (" + msgstr + ")", e);
            }
            catch (System.FormatException e)
            {
                throw new MessageParseError("Error at position (" + pos + ") while parsing msg (" + msgstr + ")", e);
            }
        }

        public static StringField ExtractField(string msgstr, ref int pos)
        {
            return ExtractField(msgstr, ref pos, null, null);
        }

        public static string ExtractBeginString(string msgstr)
        {
            int i = 0;
            return ExtractField(msgstr, ref i, null, null).Obj;
        }

        public static bool IsHeaderField(int tag)
        {
            switch (tag)
            {
                case Tags.BeginString:
                case Tags.BodyLength:
                case Tags.MsgType:
                case Tags.SenderCompID:
                case Tags.TargetCompID:
                case Tags.OnBehalfOfCompID:
                case Tags.DeliverToCompID:
                case Tags.SecureDataLen:
                case Tags.MsgSeqNum:
                case Tags.SenderSubID:
                case Tags.SenderLocationID:
                case Tags.TargetSubID:
                case Tags.TargetLocationID:
                case Tags.OnBehalfOfSubID:
                case Tags.OnBehalfOfLocationID:
                case Tags.DeliverToSubID:
                case Tags.DeliverToLocationID:
                case Tags.PossDupFlag:
                case Tags.PossResend:
                case Tags.SendingTime:
                case Tags.OrigSendingTime:
                case Tags.XmlDataLen:
                case Tags.XmlData:
                case Tags.MessageEncoding:
                case Tags.LastMsgSeqNumProcessed:
                    // case Tags.OnBehalfOfSendingTime: TODO 
                    return true;
                default:
                    return false;
            }
        }
        public static bool IsHeaderField(int tag, DataDictionary.DataDictionary dd)
        {
            if (IsHeaderField(tag))
                return true;
            if (null != dd)
                return dd.IsHeaderField(tag);
            return false;
        }

        public static bool IsTrailerField(int tag)
        {
            switch (tag)
            {
                case Tags.SignatureLength:
                case Tags.Signature:
                case Tags.CheckSum:
                    return true;
                default:
                    return false;
            }
        }
        public static bool IsTrailerField(int tag, DataDictionary.DataDictionary dd)
        {
            if (IsTrailerField(tag))
                return true;
            if (null != dd)
                return dd.IsTrailerField(tag);
            return false;
        }

        public static string GetFieldOrDefault(FieldMap fields, int tag, string defaultValue)
        {
            if (!fields.IsSetField(tag))
                return defaultValue;

            try
            {
                return fields.GetString(tag);
            }
            catch (FieldNotFoundException)
            {
                return defaultValue;
            }
        }

        public static SessionID GetReverseSessionID(Message msg)
        {
            return new SessionID(
                GetFieldOrDefault(msg.Header, Tags.BeginString, null),
                GetFieldOrDefault(msg.Header, Tags.TargetCompID, null),
                GetFieldOrDefault(msg.Header, Tags.TargetSubID, SessionID.NOT_SET),
                GetFieldOrDefault(msg.Header, Tags.TargetLocationID, SessionID.NOT_SET),
                GetFieldOrDefault(msg.Header, Tags.SenderCompID, null),
                GetFieldOrDefault(msg.Header, Tags.SenderSubID, SessionID.NOT_SET),
                GetFieldOrDefault(msg.Header, Tags.SenderLocationID, SessionID.NOT_SET)
            );
        }

        /// <summary>
        /// FIXME works, but implementation is shady
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static SessionID GetReverseSessionID(string msg)
        {
            Message FIXME = new Message(msg);
            return GetReverseSessionID(FIXME);
        }

        /// <summary>
        /// Parse the message type (tag 35) from a FIX string
        /// </summary>
        /// <param name="fixstring">the FIX string to parse</param>
        /// <returns>message type</returns>
        /// <exception cref="MessageParseError">if 35 tag is missing or malformed</exception>
        public static string GetMsgType(string fixstring)
        {
            Match match = Regex.Match(fixstring, Message.SOH + "35=([^" + Message.SOH + "]*)" + Message.SOH);
            if (match.Success)
                return match.Groups[1].Value;

            throw new MessageParseError("missing or malformed tag 35 in msg: " + fixstring);
        }

        public static ApplVerID GetApplVerID(string beginString)
        {
            switch (beginString)
            {
                case FixValues.BeginString.FIX40:
                    return new ApplVerID(ApplVerID.FIX40);
                case FixValues.BeginString.FIX41:
                    return new ApplVerID(ApplVerID.FIX41);
                case FixValues.BeginString.FIX42:
                    return new ApplVerID(ApplVerID.FIX42);
                case FixValues.BeginString.FIX43:
                    return new ApplVerID(ApplVerID.FIX43);
                case FixValues.BeginString.FIX44:
                    return new ApplVerID(ApplVerID.FIX44);
                case FixValues.BeginString.FIX50:
                    return new ApplVerID(ApplVerID.FIX50);
                case FixValues.BeginString.FIX50SP1:
                    return new ApplVerID(ApplVerID.FIX50SP1);
                case FixValues.BeginString.FIX50SP2:
                    return new ApplVerID(ApplVerID.FIX50SP2);
                default:
                    throw new System.ArgumentException(String.Format("ApplVerID for {0} not supported", beginString));
            }
        }

        #endregion

        public bool FromStringHeader(string msgstr)
        {
            Clear();
            
            int pos = 0;
            int count = 0;
            while(pos < msgstr.Length)
            {
                StringField f = ExtractField(msgstr, ref pos);
                
                if((count < 3) && (Header.HEADER_FIELD_ORDER[count++] != f.Tag))
                    return false;
                
                if(IsHeaderField(f.Tag))
                    this.Header.SetField(f, false);
                else
                    break;
            }
            return true;
        }

        /// <summary>
        /// Creates a Message from a FIX string
        /// </summary>
        /// <param name="msgstr"></param>
        /// <param name="validate"></param>
        /// <param name="sessionDD"></param>
        /// <param name="appDD"></param>
        public void FromString(string msgstr, bool validate, DataDictionary.DataDictionary sessionDD, DataDictionary.DataDictionary appDD)
        {
            this.ApplicationDataDictionary = appDD;
            FromString(msgstr, validate, sessionDD, appDD, null);
        }

        /// <summary>
        /// Create a Message from a FIX string
        /// </summary>
        /// <param name="msgstr"></param>
        /// <param name="validate"></param>
        /// <param name="sessionDD"></param>
        /// <param name="appDD"></param>
        /// <param name="msgFactory">If null, any groups will be constructed as generic Group objects</param>
        public void FromString(string msgstr, bool validate,
            DataDictionary.DataDictionary sessionDD, DataDictionary.DataDictionary appDD, IMessageFactory msgFactory)
        {
            this.ApplicationDataDictionary = appDD;
            FromString(msgstr, validate, sessionDD, appDD, msgFactory, false);
        }

        /// <summary>
        /// Creates a Message from a FIX string
        /// </summary>
        /// <param name="msgstr"></param>
        /// <param name="validate"></param>
        /// <param name="sessionDD"></param>
        /// <param name="appDD"></param>
        /// <param name="msgFactory">If null, any groups will be constructed as generic Group objects</param>
        /// <param name="ignoreBody">(default false) if true, ignores all non-header non-trailer fields.
        ///   Intended for callers that only need rejection-related information from the header.
        ///   </param>
        public void FromString(string msgstr, bool validate,
            DataDictionary.DataDictionary sessionDD, DataDictionary.DataDictionary appDD, IMessageFactory msgFactory,
            bool ignoreBody)
        {
            this.ApplicationDataDictionary = appDD;
            Clear();
            bool expectingHeader = true;
            bool expectingBody = true;
            int count = 0;
            int pos = 0;
	        DataDictionary.IFieldMapSpec msgMap = null;

            while (pos < msgstr.Length)
            {
                StringField f = ExtractField(msgstr, ref pos, sessionDD, appDD);
                
                if (validate && (count < 3) && (Header.HEADER_FIELD_ORDER[count++] != f.Tag))
                    throw new InvalidMessage("Header fields out of order");

                if (IsHeaderField(f.Tag, sessionDD))
                {
                    if (!expectingHeader)
                    {
                        if (0 == field_)
                            field_ = f.Tag;
                        validStructure_ = false;
                    }

                    if (Tags.MsgType.Equals(f.Tag))
                    {
                        string msgType = f.Obj;
                        if (appDD != null)
                        {
                            msgMap = appDD.GetMapForMessage(msgType);
                        }
		            }

                    if (!this.Header.SetField(f, false))
                        this.Header.RepeatedTags.Add(f);

                    if ((null != sessionDD) && sessionDD.Header.IsGroup(f.Tag))
                    {
                        pos = SetGroup(f, msgstr, pos, this.Header, sessionDD.Header.GetGroupSpec(f.Tag), sessionDD, appDD, msgFactory);
                    }
                }
                else if (IsTrailerField(f.Tag, sessionDD))
                {
                    expectingHeader = false;
                    expectingBody = false;
                    if (!this.Trailer.SetField(f, false))
                        this.Trailer.RepeatedTags.Add(f);

                    if ((null != sessionDD) && sessionDD.Trailer.IsGroup(f.Tag))
                    {
                        pos = SetGroup(f, msgstr, pos, this.Trailer, sessionDD.Trailer.GetGroup(f.Tag), sessionDD, appDD, msgFactory);
                    }
                }
                else if (ignoreBody==false)
                {
                    if (!expectingBody)
                    {
                        if (0 == field_)
                            field_ = f.Tag;
                        validStructure_ = false;
                    }

                    expectingHeader = false;
                    if (!SetField(f, false))
                    {
                        this.RepeatedTags.Add(f);
                    }

                    
                    if((null != msgMap) && (msgMap.IsGroup(f.Tag)))
                    {
                        pos = SetGroup(f, msgstr, pos, this, msgMap.GetGroupSpec(f.Tag), sessionDD, appDD, msgFactory);
                    }
                }
            }

            if (validate)
            {
                Validate();
            }
        }

        /// <summary>
        /// Creates a Message from FIX JSON Encoding.
        /// See: https://github.com/FIXTradingCommunity/fix-json-encoding-spec
        /// </summary>
        /// <param name="json"></param>
        /// <param name="validate"></param>
        /// <param name="sessionDD"></param>
        /// <param name="appDD"></param>
        /// <param name="msgFactory">If null, any groups will be constructed as generic Group objects</param>
        public void FromJson(string json, bool validate, DataDictionary.DataDictionary sessionDD, DataDictionary.DataDictionary appDD, IMessageFactory msgFactory)
        {
            this.ApplicationDataDictionary = appDD;
            Clear();

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                string beginString = document.RootElement.GetProperty("Header").GetProperty("BeginString").GetString();
                string msgType     = document.RootElement.GetProperty("Header").GetProperty("MsgType").GetString();
                DataDictionary.IFieldMapSpec msgMap = appDD.GetMapForMessage(msgType);
                FromJson(document.RootElement.GetProperty("Header"),  beginString, msgType, msgMap, msgFactory, sessionDD, this.Header);
                FromJson(document.RootElement.GetProperty("Body"),    beginString, msgType, msgMap, msgFactory, appDD,     this);
                FromJson(document.RootElement.GetProperty("Trailer"), beginString, msgType, msgMap, msgFactory, sessionDD, this.Trailer);
            }

            this.Header.SetField(new BodyLength(BodyLength()), true);
            this.Trailer.SetField(new CheckSum(Fields.Converters.CheckSumConverter.Convert(CheckSum())), true);

            if (validate)
            {
                Validate();
            }
        }

        protected void FromJson(JsonElement jsonElement, string beginString, string msgType, DataDictionary.IFieldMapSpec msgMap, IMessageFactory msgFactory, DataDictionary.DataDictionary dataDict, FieldMap fieldMap)
        {
            foreach (JsonProperty field in jsonElement.EnumerateObject())
            {
                DataDictionary.DDField ddField;
                if (dataDict.FieldsByName.TryGetValue(field.Name.ToString(), out ddField))
                {
                    if ((null != msgMap) && (msgMap.IsGroup(ddField.Tag)) && (JsonValueKind.Array == field.Value.ValueKind))
                    {
                        foreach (JsonElement jsonGrp in field.Value.EnumerateArray())
                        {
                            Group grp = msgFactory.Create(beginString, msgType, ddField.Tag);
                            FromJson(jsonGrp, beginString, msgType, msgMap.GetGroupSpec(ddField.Tag), msgFactory, dataDict, grp);
                            fieldMap.AddGroup(grp);
                        }
                    }

                    if (JsonValueKind.Array != field.Value.ValueKind)
                    {
                        fieldMap.SetField(new StringField(ddField.Tag, field.Value.ToString()));
                    }
                }
                else
                {
                    // this may be a custom tag given by number instead of name
                    if (Int32.TryParse(field.Name.ToString(), out int customTagNumber))
                    {
                        fieldMap.SetField(new StringField(customTagNumber, field.Value.ToString()));
                    }
                }
            }
        }

        /// <summary>
        /// Constructs a group and stores it in this Message object
        /// </summary>
        /// <param name="grpNoFld">the group's counter field</param>
        /// <param name="msgstr">full message string</param>
        /// <param name="pos">starting character position of group</param>
        /// <param name="fieldMap">full message as FieldMap</param>
        /// <param name="groupDD">group definition structure from dd</param>
        /// <param name="sessionDataDictionary"></param>
        /// <param name="appDD"></param>
        /// <param name="msgFactory">if null, then this method will use the generic Group class constructor</param>
        /// <returns></returns>
        protected int SetGroup(
            StringField grpNoFld, string msgstr, int pos, FieldMap fieldMap, DataDictionary.IGroupSpec groupDD,
            DataDictionary.DataDictionary sessionDataDictionary, DataDictionary.DataDictionary appDD, IMessageFactory msgFactory)
        {
            int grpEntryDelimiterTag = groupDD.Delim;
            int grpPos = pos;
            Group grp = null; // the group entry being constructed

            while (pos < msgstr.Length)
            {
                grpPos = pos;
                StringField f = ExtractField(msgstr, ref pos, sessionDataDictionary, appDD);
                if (f.Tag == grpEntryDelimiterTag)
                {
                    // This is the start of a group entry.

                    if (grp != null)
                    {
                        // We were already building an entry, so the delimiter means it's done.
                        fieldMap.AddGroup(grp, false);
                        grp = null; // prepare for new Group
                    }

                    // Create a new group!
                    if (msgFactory != null)
                        grp = msgFactory.Create(Message.ExtractBeginString(msgstr), Message.GetMsgType(msgstr), grpNoFld.Tag);

                    //If above failed (shouldn't ever happen), just use a generic Group.
                    if (grp == null)
                        grp = new Group(grpNoFld.Tag, grpEntryDelimiterTag);
                }
                else if (!groupDD.IsField(f.Tag))
                {
                    // This field is not in the group, thus the repeating group is done.
                    if (grp != null)
                    {
                        fieldMap.AddGroup(grp, false);
                    }
                    return grpPos;
                }
                else if(groupDD.IsField(f.Tag) && grp != null && grp.IsSetField(f.Tag))
                {
                    // Tag is appearing for the second time within a group element.
                    // Presumably the sender didn't set the delimiter (or their DD has a different delimiter).
                    throw new RepeatedTagWithoutGroupDelimiterTagException(grpNoFld.Tag, f.Tag);
                }

                if (grp == null)
                {
                    // This means we got into the group's fields without finding a delimiter tag.
                    throw new GroupDelimiterTagException(grpNoFld.Tag, grpEntryDelimiterTag);
                }

                // f is just a field in our group entry.  Add it and iterate again.
                grp.SetField(f);
                if(groupDD.IsGroup(f.Tag))
                {
                    // f is a counter for a nested group.  Recurse!
                    pos = SetGroup(f, msgstr, pos, grp, groupDD.GetGroupSpec(f.Tag), sessionDataDictionary, appDD, msgFactory);
                }
            }
            
            return grpPos;
        }

        public bool HasValidStructure(out int field)
        {
            field = field_;
            return validStructure_;
        }

        public void Validate()
        {
            try
            {
                int receivedBodyLength = this.Header.GetInt(Tags.BodyLength);
                if (BodyLength() != receivedBodyLength)
                    throw new InvalidMessage("Expected BodyLength=" + BodyLength() + ", Received BodyLength=" + receivedBodyLength + ", Message.SeqNum=" + this.Header.GetInt(Tags.MsgSeqNum));

                int receivedCheckSum = this.Trailer.GetInt(Tags.CheckSum);
                if (CheckSum() != receivedCheckSum)
                    throw new InvalidMessage("Expected CheckSum=" + CheckSum() + ", Received CheckSum=" + receivedCheckSum + ", Message.SeqNum=" + this.Header.GetInt(Tags.MsgSeqNum));
            }
            catch (FieldNotFoundException e)
            {
                throw new InvalidMessage("BodyLength or CheckSum missing", e);
            }
            catch (FieldConvertError e)
            {
                throw new InvalidMessage("BodyLength or Checksum has wrong format", e);
            }
        }

        public void ReverseRoute(Header header)
        {
            // required routing tags
            this.Header.RemoveField(Tags.BeginString);
            this.Header.RemoveField(Tags.SenderCompID);
            this.Header.RemoveField(Tags.SenderSubID);
            this.Header.RemoveField(Tags.SenderLocationID);
            this.Header.RemoveField(Tags.TargetCompID);
            this.Header.RemoveField(Tags.TargetSubID);
            this.Header.RemoveField(Tags.TargetLocationID);

            if (header.IsSetField(Tags.BeginString))
            {
                string beginString = header.GetString(Tags.BeginString);
                if (beginString.Length > 0)
                    this.Header.SetField(new BeginString(beginString));

                this.Header.RemoveField(Tags.OnBehalfOfLocationID);
                this.Header.RemoveField(Tags.DeliverToLocationID);

                if (string.CompareOrdinal(beginString, "FIX.4.1") >= 0)
                {
                    if (header.IsSetField(Tags.OnBehalfOfLocationID))
                    {
                        string onBehalfOfLocationID = header.GetString(Tags.OnBehalfOfLocationID);
                        if (onBehalfOfLocationID.Length > 0)
                            this.Header.SetField(new DeliverToLocationID(onBehalfOfLocationID));
                    }

                    if (header.IsSetField(Tags.DeliverToLocationID))
                    {
                        string deliverToLocationID = header.GetString(Tags.DeliverToLocationID);
                        if (deliverToLocationID.Length > 0)
                            this.Header.SetField(new OnBehalfOfLocationID(deliverToLocationID));
                    }
                }
            }

            if (header.IsSetField(Tags.SenderCompID))
            {
                SenderCompID senderCompID = new SenderCompID();
                header.GetField(senderCompID);
                if (senderCompID.Obj.Length > 0)
                    this.Header.SetField(new TargetCompID(senderCompID.Obj));
            }

            if (header.IsSetField(Tags.SenderSubID))
            {
                SenderSubID senderSubID = new SenderSubID();
                header.GetField(senderSubID);
                if (senderSubID.Obj.Length > 0)
                    this.Header.SetField(new TargetSubID(senderSubID.Obj));
            }

            if (header.IsSetField(Tags.SenderLocationID))
            {
                SenderLocationID senderLocationID = new SenderLocationID();
                header.GetField(senderLocationID);
                if (senderLocationID.Obj.Length > 0)
                    this.Header.SetField(new TargetLocationID(senderLocationID.Obj));
            }

            if (header.IsSetField(Tags.TargetCompID))
            {
                TargetCompID targetCompID = new TargetCompID();
                header.GetField(targetCompID);
                if (targetCompID.Obj.Length > 0)
                    this.Header.SetField(new SenderCompID(targetCompID.Obj));
            }

            if (header.IsSetField(Tags.TargetSubID))
            {
                TargetSubID targetSubID = new TargetSubID();
                header.GetField(targetSubID);
                if (targetSubID.Obj.Length > 0)
                    this.Header.SetField(new SenderSubID(targetSubID.Obj));
            }

            if (header.IsSetField(Tags.TargetLocationID))
            {
                TargetLocationID targetLocationID = new TargetLocationID();
                header.GetField(targetLocationID);
                if (targetLocationID.Obj.Length > 0)
                    this.Header.SetField(new SenderLocationID(targetLocationID.Obj));
            }

            // optional routing tags
            this.Header.RemoveField(Tags.OnBehalfOfCompID);
            this.Header.RemoveField(Tags.OnBehalfOfSubID);
            this.Header.RemoveField(Tags.DeliverToCompID);
            this.Header.RemoveField(Tags.DeliverToSubID);

            if(header.IsSetField(Tags.OnBehalfOfCompID))
            {
                string onBehalfOfCompID = header.GetString(Tags.OnBehalfOfCompID);
                if(onBehalfOfCompID.Length > 0)
                    this.Header.SetField(new DeliverToCompID(onBehalfOfCompID));
            }

            if(header.IsSetField(Tags.OnBehalfOfSubID))
            {
                string onBehalfOfSubID = header.GetString(  Tags.OnBehalfOfSubID);
                if(onBehalfOfSubID.Length > 0)
                    this.Header.SetField(new DeliverToSubID(onBehalfOfSubID));
            }

            if(header.IsSetField(Tags.DeliverToCompID))
            {
                string deliverToCompID = header.GetString(Tags.DeliverToCompID);
                if(deliverToCompID.Length > 0)
                    this.Header.SetField(new OnBehalfOfCompID(deliverToCompID));
            }

            if(header.IsSetField(Tags.DeliverToSubID))
            {
                string deliverToSubID = header.GetString(Tags.DeliverToSubID);
                if(deliverToSubID.Length > 0)
                    this.Header.SetField(new OnBehalfOfSubID(deliverToSubID));
            }
        }

        public int CheckSum()
        {
            return (
                (this.Header.CalculateTotal()
                + CalculateTotal()
                + this.Trailer.CalculateTotal()) % 256);
        }

        public bool IsAdmin()
        {
            return this.Header.IsSetField(Tags.MsgType) && IsAdminMsgType(this.Header.GetString(Tags.MsgType));
        }

        public bool IsApp()
        {
            return this.Header.IsSetField(Tags.MsgType) && !IsAdminMsgType(this.Header.GetString(Tags.MsgType));
        }

        /// <summary>
        /// FIXME less operator new
        /// </summary>
        /// <param name="sessionID"></param>
        public void SetSessionID(SessionID sessionID)
        {
            this.Header.SetField(new BeginString(sessionID.BeginString));
            this.Header.SetField(new SenderCompID(sessionID.SenderCompID));
            if (SessionID.IsSet(sessionID.SenderSubID))
                this.Header.SetField(new SenderSubID(sessionID.SenderSubID));
            if (SessionID.IsSet(sessionID.SenderLocationID))
                this.Header.SetField(new SenderLocationID(sessionID.SenderLocationID));
            this.Header.SetField(new TargetCompID(sessionID.TargetCompID));
            if (SessionID.IsSet(sessionID.TargetSubID))
                this.Header.SetField(new TargetSubID(sessionID.TargetSubID));
            if (SessionID.IsSet(sessionID.TargetLocationID))
                this.Header.SetField(new TargetLocationID(sessionID.TargetLocationID));
        }

        public SessionID GetSessionID(Message m)
        {
            bool senderSubIDSet = m.Header.IsSetField(Tags.SenderSubID);
            bool senderLocationIDSet = m.Header.IsSetField(Tags.SenderLocationID);
            bool targetSubIDSet = m.Header.IsSetField(Tags.TargetSubID);
            bool targetLocationIDSet = m.Header.IsSetField(Tags.TargetLocationID);

            if (senderSubIDSet && senderLocationIDSet && targetSubIDSet && targetLocationIDSet)
                return new SessionID(m.Header.GetString(Tags.BeginString),
                    m.Header.GetString(Tags.SenderCompID), m.Header.GetString(Tags.SenderSubID), m.Header.GetString(Tags.SenderLocationID),
                    m.Header.GetString(Tags.TargetCompID), m.Header.GetString(Tags.TargetSubID), m.Header.GetString(Tags.TargetLocationID));
            else if (senderSubIDSet && targetSubIDSet)
                return new SessionID(m.Header.GetString(Tags.BeginString),
                    m.Header.GetString(Tags.SenderCompID), m.Header.GetString(Tags.SenderSubID),
                    m.Header.GetString(Tags.TargetCompID), m.Header.GetString(Tags.TargetSubID));
            else
                return new SessionID(
                    m.Header.GetString(Tags.BeginString),
                    m.Header.GetString(Tags.SenderCompID),
                    m.Header.GetString(Tags.TargetCompID));
        }

        public override void Clear()
        {
            field_ = 0;
            this.Header.Clear();
            base.Clear();
            this.Trailer.Clear();
        }

        private Object lock_ToString = new Object();
        public override string ToString()
        {
            lock (lock_ToString)
            {
                this.Header.SetField(new BodyLength(BodyLength()), true);
                this.Trailer.SetField(new CheckSum(Fields.Converters.CheckSumConverter.Convert(CheckSum())), true);

                return this.Header.CalculateString() + CalculateString() + this.Trailer.CalculateString();
            }
        }

        protected int BodyLength()
        {
            return this.Header.CalculateLength() + CalculateLength() + this.Trailer.CalculateLength();
        }

        private static string FieldMapToXML(DataDictionary.DataDictionary dd, FieldMap fields, int space)
        {
            StringBuilder s = new StringBuilder();

            // fields
            foreach (var f in fields)
            {
               s.Append("<field ");
               if ((dd != null) && ( dd.FieldsByTag.ContainsKey(f.Key)))
               {
                   s.Append("name=\"" + dd.FieldsByTag[f.Key].Name + "\" ");
               }
               s.Append("number=\"" + f.Key.ToString() + "\">");
               s.Append("<![CDATA[" + f.Value.ToString() + "]]>");
               s.Append("</field>");
            }
            // now groups
            List<int> groupTags = fields.GetGroupTags();
            foreach (int groupTag in groupTags)
            {
                for (int counter = 1; counter <= fields.GroupCount(groupTag); counter++)
                {
                    s.Append("<group>");
                    s.Append(FieldMapToXML(dd, fields.GetGroup(counter, groupTag), space+1));
                    s.Append("</group>");
                }
            }

            return s.ToString();
        }


        /// <summary>
        /// ToJSON() helper method.
        /// </summary>
        /// <returns>an XML string</returns>
        private static StringBuilder FieldMapToJSON(StringBuilder sb, DataDictionary.DataDictionary dd, FieldMap fields, bool humanReadableValues)
        {
            IList<int> numInGroupTagList = fields.GetGroupTags();
            IList<Fields.IField> numInGroupFieldList = new List<Fields.IField>();

            // Non-Group Fields
            foreach (var field in fields)
            {
                if (QuickFix.Fields.CheckSum.TAG == field.Value.Tag)
                    continue; // FIX JSON Encoding does not include CheckSum

                if (numInGroupTagList.Contains(field.Value.Tag))
                {
                    numInGroupFieldList.Add(field.Value);
                    continue; // Groups will be handled below
                }

                if ((dd != null) && (dd.FieldsByTag.ContainsKey(field.Value.Tag)))
                {
                    sb.Append("\"" + dd.FieldsByTag[field.Value.Tag].Name + "\":");
                    if (humanReadableValues)
                    {
                        string valueDescription;
                        if (dd.FieldsByTag[field.Value.Tag].EnumDict.TryGetValue(field.Value.ToString(), out valueDescription))
                        {
                            sb.Append("\"" + valueDescription + "\",");
                        }
                        else
                            sb.Append("\"" + field.Value.ToString() + "\",");
                    }
                    else
                    {
                        sb.Append("\"" + field.Value.ToString() + "\",");
                    }
                }
                else
                {
                    sb.Append("\"" + field.Value.Tag.ToString() + "\":");
                    sb.Append("\"" + field.Value.ToString() + "\",");
                }
            }

            // Group Fields
            foreach (Fields.IField numInGroupField in numInGroupFieldList)
            {
                // The name of the NumInGroup field is the key of the JSON list containing the Group items
                if ((dd != null) && ( dd.FieldsByTag.ContainsKey(numInGroupField.Tag)))
                    sb.Append("\"" + dd.FieldsByTag[numInGroupField.Tag].Name + "\":[");
                else
                    sb.Append("\"" + numInGroupField.Tag.ToString() + "\":[");

                // Populate the JSON list with the Group items
                for (int counter = 1; counter <= fields.GroupCount(numInGroupField.Tag); counter++)
                {
                    sb.Append('{');
                    FieldMapToJSON(sb, dd, fields.GetGroup(counter, numInGroupField.Tag), humanReadableValues);
                    sb.Append("},");
                }

                // Remove trailing comma
                if (sb.Length > 0 && sb[sb.Length - 1] == ',')
                    sb.Remove(sb.Length - 1, 1);

                sb.Append("],");
            }
            // Remove trailing comma
            if (sb.Length > 0 && sb[sb.Length - 1] == ',')
                sb.Remove(sb.Length - 1, 1);

            return sb;
        }

        /// <summary>
        /// Get a representation of the message as an XML string.
        /// (NOTE: this is just an ad-hoc XML; it is NOT FIXML.)
        /// </summary>
        /// <returns>an XML string</returns>
        public string ToXML()
        {
            return ToXML(this.ApplicationDataDictionary);
        }

        /// <summary>
        /// Get a representation of the message as an XML string.
        /// (NOTE: this is just an ad-hoc XML; it is NOT FIXML.)
        /// </summary>
        /// <returns>an XML string</returns>
        public string ToXML(DataDictionary.DataDictionary dd)
        {
            StringBuilder s = new StringBuilder();
            s.Append("<message>");
            s.Append("<header>");
            s.Append(FieldMapToXML(dd, Header, 4));
            s.Append("</header>");
            s.Append("<body>");
            s.Append(FieldMapToXML(dd, this, 4));
            s.Append("</body>");
            s.Append("<trailer>");
            s.Append(FieldMapToXML(dd, Trailer, 4));
            s.Append("</trailer>");
            s.Append("</message>");
            return s.ToString();
        }

        /// <summary>
        /// Get a representation of the message as a string in FIX JSON Encoding.
        /// See: https://github.com/FIXTradingCommunity/fix-json-encoding-spec
        /// </summary>
        /// <returns>a JSON string</returns>
        public string ToJSON()
        {
            return ToJSON(this.ApplicationDataDictionary, false);
        }


        /// <summary>
        /// Get a representation of the message as a string in FIX JSON Encoding.
        /// See: https://github.com/FIXTradingCommunity/fix-json-encoding-spec
        ///
        /// Per the FIX JSON Encoding spec, tags are converted to human-readable form, but values are not.
        /// If you want human-readable values, set humanReadableValues to true.
        /// </summary>
        /// <returns>a JSON string</returns>
        public string ToJSON(bool humanReadableValues)
        {
            return ToJSON(this.ApplicationDataDictionary, humanReadableValues);
        }

        /// <summary>
        /// Get a representation of the message as a string in FIX JSON Encoding.
        /// See: https://github.com/FIXTradingCommunity/fix-json-encoding-spec
        ///
        /// Per the FIX JSON Encoding spec, tags are converted to human-readable form, but values are not.
        /// If you want human-readable values, set humanReadableValues to true.
        /// </summary>
        /// <returns>a JSON string</returns>
        public string ToJSON(DataDictionary.DataDictionary dd, bool humanReadableValues)
        {
            StringBuilder sb = new StringBuilder().Append('{').Append("\"Header\":{");
            FieldMapToJSON(sb, dd, Header,  humanReadableValues).Append("},\"Body\":{");
            FieldMapToJSON(sb, dd, this,    humanReadableValues).Append("},\"Trailer\":{");
            FieldMapToJSON(sb, dd, Trailer, humanReadableValues).Append("}}");
            return sb.ToString();
        }
    }
}
