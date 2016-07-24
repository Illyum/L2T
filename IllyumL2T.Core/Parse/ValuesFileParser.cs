﻿using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

using IllyumL2T.Core.Interfaces;
using IllyumL2T.Core.FieldsSplit;

namespace IllyumL2T.Core.Parse
{
  public abstract class ValuesFileParser<T> where T : class, new()
  {
    protected ILineParser<T> _lineParser;

    public ValuesFileParser()
    {
      _lineParser = new LineParser<T>();
    }

    #region BinaryReader-based processing.

    /// <summary>
    /// The case for multi-byte separators is not supported yet.
    /// A reference http://www.asciitable.com
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="group_separator">Optional.</param>
    /// <param name="record_separator">Optional.</param>
    /// <param name="unit_separator">Optional.</param>
    /// <returns></returns>
    public IEnumerable<ParseResult<T>> Read(BinaryReader reader, byte? group_separator = null, byte? record_separator = null, byte? unit_separator = null)
    {
      return ReadBinaryAsTemplateMethod(reader, group_separator, record_separator, unit_separator);
    }

    protected IEnumerable<ParseResult<T>> ReadBinaryAsTemplateMethod(BinaryReader reader, byte? group_separator, byte? record_separator, byte? unit_separator)
    {
      if (reader == null)
      {
        throw new ArgumentNullException($"{nameof(BinaryReader)} reader");
      }

      FieldsSplitterBase<T> fieldsSplitter = CreateValuesFieldsSplitter(); //simplest initial positional case, with no separators or delimiters whatsoever.
      var lineParser = new LineParser<T>(fieldsSplitter);
      return ReadBytesAsBinary(reader, lineParser);
    }

    private IEnumerable<ParseResult<T>> ReadBytesAsBinary(BinaryReader reader, ILineParser<T> lineParser)
    {
      var marshaller = new Marshaller<T>(lineParser);
      foreach (var result in marshaller.Read(new PacketReader(reader)))
      {
        yield return result;
      }
    }

    /// <summary>
    /// Not yet used.
    /// It would cover four combinations:
    /// Packet layout : fixed-length or delimited
    /// Message layout: fixed-length or delimited
      //TODO: For byte[] parsing, be aware that bytes could be deserialized as System.String or as a .NET ValueType.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="group_separator"></param>
    /// <param name="record_separator"></param>
    /// <param name="unit_separator"></param>
    /// <returns></returns>
    private IEnumerable<ParseResult<T>> ReadBytesAsBinary(BinaryReader reader, byte? group_separator, byte? record_separator, byte? unit_separator)
    {
      var marshaller = new Marshaller<T>(null);
      foreach (var result in marshaller.Read(new PacketReader(reader)))
      {
        yield return result;
      }
    }
    #endregion

    #region TextReader-based processing.

    /// <summary>
    /// Parses delimiter-separated values from text lines.
    /// </summary>
    /// <param name="reader">Input reader of text lines.</param>
    /// <param name="delimiter">Delimites values in text lines.</param>
    /// <param name="includeHeaders">Are there headers in the first input line?</param>
    /// <returns>Iterator with the parsed results.</returns>
    public IEnumerable<ParseResult<T>> Read(TextReader reader, char delimiter, bool includeHeaders)
    {
      return ReadTextAsTemplateMethod(reader, includeHeaders: includeHeaders, delimiter: delimiter);
    }

    /// <summary>
    /// Parses positional/fixed-width values from text lines.
    /// </summary>
    /// <param name="reader">Input reader of text lines.</param>
    /// <param name="includeHeaders">Are there headers in the first input line?</param>
    /// <returns>Iterator with the parsed results.</returns>
    public IEnumerable<ParseResult<T>> Read(TextReader reader, bool includeHeaders)
    {
      return ReadTextAsTemplateMethod(reader, includeHeaders: includeHeaders);
    }

    /// <summary>
    /// Defines the abstract (basic) structure of the textline-based parsing algorithm. The specific field splitting policy is deferred to subclasses.
    /// By Template Method we mean, quote: Define the skeleton of an algorithm in an operation, deferring some steps to subclasses. Template Method lets subclasses redefine certain steps of an algorithm without changing the algorithm's structure.
    /// http://www.dofactory.com/net/template-method-design-pattern
    /// </summary>
    /// <param name="reader">Input reader of text lines.</param>
    /// <param name="includeHeaders">Are there headers in the first input line?</param>
    /// <param name="delimiter">Optional. Used for the case of delimited field splitting policy.</param>
    /// <returns>Iterator with the parsed results.</returns>
    protected IEnumerable<ParseResult<T>> ReadTextAsTemplateMethod(TextReader reader, bool includeHeaders, char? delimiter = null)
    {
      if (reader == null)
      {
        throw new ArgumentNullException($"{nameof(TextReader)} reader");
      }

      // If the file contains headers in the first row, simply skip those...
      if (includeHeaders)
      {
        reader.ReadLine();
      }

      FieldsSplitterBase<T> fieldsSplitter = CreateValuesFieldsSplitter(delimiter);
      var lineParser = new LineParser<T>(fieldsSplitter);

      while (true)
      {
        var line = reader.ReadLine();
        if (String.IsNullOrEmpty(line) == false)
        {
          var parseResult = lineParser.Parse(line);
          yield return parseResult;
        }
        else
        {
          yield break;
        }
      }
    }

    #endregion

    /// <summary>
    /// When overridden in a derived class, gets the proper policy for field splitting.
    /// </summary>
    /// <param name="delimiter">Optional. Delimiter for delimiter-separated values.</param>
    /// <returns>Class for field splitting policy.</returns>
    protected abstract FieldsSplitterBase<T> CreateValuesFieldsSplitter(char? delimiter = null);
  }

  //TODO:Temporally here. Once evaluated, then they could move at a proper location.
  #region bytes as binary
  public interface IPacketReader
  {
    IEnumerable<byte[]> ReadNextPacket();
  }
  public interface IMessageReader
  {
    IEnumerable<byte[]> ReadNextMessageFrom(byte[] packet);
  }
  public class Marshaller<T> where T : class, new()
  {
    private Type SchemaToRead;
    private int message_size;
    private ILineParser<T> lineParser;

    public Marshaller(ILineParser<T> lineParser)
    {
      this.lineParser = lineParser;
      SchemaToRead = typeof(T);
      message_size = CalculateTotalDeclaredSize(SchemaToRead);
    }

    public IEnumerable<ParseResult<T>> Read(IPacketReader packetReader)
    {
      IMessageReader messageReader = new MessageReader((uint)message_size);

      foreach (byte[] packet in packetReader.ReadNextPacket())
      {
        foreach (byte[] message in messageReader.ReadNextMessageFrom(packet))
        {
          string message_line = Provisional_ForExploratoryPurposes_DefaultToUTF8String(message);
          yield return lineParser.Parse(message_line);

          //string[] values = ParseAsStrings(message);
          //IEnumerable<byte[]> values = ReadAsBytesArrays(message);
          //var messageParser = new MessageParser<T>();
          //yield return messageParser.Parse(values, message);

        }
      }
    }

    /// <summary>
    /// A first try to link with the existing design.
    /// </summary>
    /// <param name="message">Data message</param>
    /// <returns>Values to be parsed by a LineParser-derived class.</returns>
    public string[] ParseAsStrings(byte[] message)
    {
      var result = new List<string>();
      var buffer = new CircularBuffer((uint)message.Length);
      buffer.Add(message);

      foreach (uint bytes_to_read in GetPropertyLengths(SchemaToRead))
      {
        string dataitem_as_string = string.Empty;
        if (bytes_to_read > 0)
        {
          byte[] dataitem_bytes = buffer.ReadBytes(bytes_to_read);
          if (dataitem_bytes == null)
          {
            throw new Exception($"No {bytes_to_read} bytes found for dataitem. Message Length: {message.Length}");
          }
          dataitem_as_string = Provisional_ForExploratoryPurposes_DefaultToUTF8String(dataitem_bytes);
        }
        result.Add(dataitem_as_string);
      }
      return result.ToArray();
    }

    public IEnumerable<byte[]> ReadAsBytesArrays(byte[] message)
    {
      //TODO: analyze input validation

      var result = new List<byte[]>();
      var buffer = new CircularBuffer((uint)message.Length);
      buffer.Add(message);

      foreach (uint bytes_to_read in GetPropertyLengths(SchemaToRead))
      {
        byte[] dataitem_bytes = null;
        if (bytes_to_read > 0)
        {
          dataitem_bytes = buffer.ReadBytes(bytes_to_read);
          if (dataitem_bytes == null)
          {
            throw new Exception($"No {bytes_to_read} bytes found for dataitem. Message Length: {message.Length}");
          }
        }
        result.Add(dataitem_bytes);
      }
      return result;
    }

    private int CalculateTotalDeclaredSize(Type type) => type.GetProperties().Select(p => (IllyumL2T.Core.ParseBehaviorAttribute)p.GetCustomAttributes(typeof(IllyumL2T.Core.ParseBehaviorAttribute), true).First()).Where(attr => attr.Length > 0).Sum(a => a.Length);
    private IEnumerable<uint> GetPropertyLengths(Type type) => type.GetProperties().Select(p => (IllyumL2T.Core.ParseBehaviorAttribute)p.GetCustomAttributes(typeof(IllyumL2T.Core.ParseBehaviorAttribute), true).First()).Select(attr => (uint)attr.Length);
    private string Provisional_ForExploratoryPurposes_DefaultToUTF8String(byte[] dataitem)
    {
      //TODO: check the possible requirements for binary vs text declarations.
      return System.Text.Encoding.UTF8.GetString(dataitem);
    }
  }

  public class PacketReader : IPacketReader
  {
    private System.IO.BinaryReader source;

    public PacketReader(System.IO.BinaryReader reader)
    {
      source = reader;
    }

    /// <summary>
    /// From a given binary source, it provides all contained data packets from that binary source.
    /// A raw data packet could contain zero o more application messages or a fragment of an application message at the end of a data packet.
    /// </summary>
    /// <returns>Iterator for all raw data packets from a given binary source.</returns>
    public IEnumerable<byte[]> ReadNextPacket()
    {
      const int buffer_size = 0x400; //Question: What if the incoming packet is larger than this value? Answer: By the Marshaller<T>.Read method, as currently designed, such larger packet will be processed the same as a smaller one: each contained message will be processed and any fragmented message at the end of the packet would be completed with the completing fragment at the start of the next read packet. Moreover, in the case of a larger message inside a larger packet, the same applies, in principle, thanks to the adjusting size of CircularBuffer. Of course, execution-based evidence is needed to backup just that.
      byte[] buffer = new byte[buffer_size];
      do
      {
        int read = source.Read(buffer, 0, buffer_size);
System.Diagnostics.Trace.WriteLine($"read:{read}");
        if (read == 0)
        {
          yield break;
        }
        byte[] packet = new byte[read];
        Array.Copy(buffer, packet, read);
        yield return packet;
      } while (true);
    }
  }
  public class MessageReader : IMessageReader
  {
    private CircularBuffer buffer;
    private uint message_size;

    public MessageReader(uint message_size) : this(new CircularBuffer(), message_size) { }

    public MessageReader(CircularBuffer buffer, uint message_size)
    {
      this.buffer = buffer;
      this.message_size = message_size;
    }

    public IEnumerable<byte[]> ReadNextMessageFrom(byte[] packet)
    {
      buffer.Add(packet);
      do //to fetch all messages from the packet
      {
        uint next_message_size = message_size;
        byte[] rawMessage = buffer.ReadBytes(next_message_size);
        if (rawMessage == null)
        {
          yield break;
        }
        yield return rawMessage;
      } while (true);
    }
  }
  public class CircularBuffer //TODO: There are a bunch of unit tests for this class. Once evaluated here, those unit test must be added here as well.
  {
    public uint CurrentMaxSize;
    public uint CurrentSize;
    private byte[] buffer;
    private uint write_index;
    private uint read_index;

    public CircularBuffer(uint initialMaxSize = 0x4800)
    {
      CurrentMaxSize = initialMaxSize;
      buffer = new byte[CurrentMaxSize];
      write_index = 0;
      read_index = 0;
      CurrentSize = 0;
    }

    public void Add(byte[] data)
    {
      if (data == null || data.Length <= 0)
      {
        return;
      }
      uint free_bytes = CurrentMaxSize - CurrentSize;
      if (data.Length > free_bytes)
      {
        uint newCurrentMaxSize = CurrentMaxSize + ((uint)data.Length - free_bytes);
        byte[] newbuffer = new byte[newCurrentMaxSize];
        if (CurrentSize > 0)
        {
          byte[] current_content = InternalReadBytes(CurrentSize);
          Array.Copy(current_content, 0, newbuffer, 0, CurrentSize);
        }
        read_index = 0;
        write_index = CurrentSize;
        buffer = newbuffer;
        CurrentMaxSize = newCurrentMaxSize;
      }
      if (write_index + data.Length <= CurrentMaxSize)
      {
        Array.Copy(data, 0, buffer, write_index, data.Length);
      }
      else
      {
        uint this_cycle_remain = CurrentMaxSize - write_index;
        uint next_cycle_fragment = (uint)data.Length - this_cycle_remain;
        Array.Copy(data, 0, buffer, write_index, this_cycle_remain);
        Array.Copy(data, this_cycle_remain, buffer, 0, next_cycle_fragment);
      }
      CurrentSize += (uint)data.Length;
      write_index = (write_index + (uint)data.Length) % CurrentMaxSize;
    }

    public byte[] InternalReadBytes(uint size)
    {
      byte[] result = null;
      if (size > 0 && size <= CurrentSize)
      {
        result = GetBytes(size);
      }
      return result;
    }

    private byte[] GetBytes(uint size)
    {
      byte[] result = null;
      if (size > 0)
      {
        result = new byte[size];
        if (read_index + size <= CurrentMaxSize)
        {
          Array.Copy(buffer, read_index, result, 0, size);
        }
        else
        {
          uint this_cycle_remain = CurrentMaxSize - read_index;
          uint next_cycle_fragment = size - this_cycle_remain;
          Array.Copy(buffer, read_index, result, 0, this_cycle_remain);
          Array.Copy(buffer, 0, result, this_cycle_remain, next_cycle_fragment);
        }
      }
      return result;
    }

    public byte[] ReadBytes(uint size)
    {
      byte[] result = null;
      if (size > 0)
      {
        result = InternalReadBytes(size);
        if (result != null)
        {
          CurrentSize -= size;
          read_index = (read_index + size) % CurrentMaxSize;
        }
      }
      return result;
    }
  }
  #endregion
}