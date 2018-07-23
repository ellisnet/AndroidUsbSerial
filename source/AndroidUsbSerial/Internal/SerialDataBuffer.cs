using System;
using System.IO;

namespace AndroidUsbSerial.Internal
{
    public enum DataBufferMode
    {
        Read,
        Write,
    }

    public class SerialDataBuffer
    {
        private DataBufferMode _bufferMode;

        private MemoryStream _stream;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        private SerialDataBuffer()
        {
            _stream = new MemoryStream();
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);
        }

        ~SerialDataBuffer()
        {
            _reader.Close();
            _writer.Close();
            _stream.Close();
            _stream.Dispose();
        }

        public static SerialDataBuffer Allocate(int capacity) => new SerialDataBuffer
            {
                _stream = { Capacity = capacity },
                _bufferMode = DataBufferMode.Write
            };

        public static SerialDataBuffer AllocateDirect(int capacity) => Allocate(capacity);

        public int Capacity => _stream.Capacity;

        public SerialDataBuffer Flip()
        {
            _bufferMode = DataBufferMode.Read;
            _stream.SetLength(_stream.Position);
            _stream.Position = 0;
            return this;
        }

        public SerialDataBuffer Clear()
        {
            _bufferMode = DataBufferMode.Write;
            _stream.Position = 0;
            return this;
        }

        public SerialDataBuffer Compact()
        {
            _bufferMode = DataBufferMode.Write;
            var newStream = new MemoryStream(_stream.Capacity);
            _stream.CopyTo(newStream);
            _stream = newStream;
            return this;
        }

        public SerialDataBuffer Rewind()
        {
            _stream.Position = 0;
            return this;
        }

        public long Limit => (_bufferMode == DataBufferMode.Write)
            ? _stream.Capacity
            : _stream.Length;

        public long Position => _stream.Position;

        public SerialDataBuffer SetPosition(long newPosition)
        {
            _stream.Position = newPosition;
            return this;
        }

        public long Remaining => Limit - Position;

        public bool HasRemaining => Remaining > 0;

        public int GetNext() => _stream.ReadByte();

        public SerialDataBuffer Get(byte[] destination, int offset, int length)
        {
            _stream.Read(destination, offset, length);
            return this;
        }

        public SerialDataBuffer Get(byte[] dst, long offset, long length)
        {
            if (offset > Int32.MaxValue) { throw new ArgumentOutOfRangeException(nameof(offset)); }
            if (length > Int32.MaxValue) { throw new ArgumentOutOfRangeException(nameof(length)); }

            _stream.Read(dst, (int)offset, (int)length);
            return this;
        }

        public SerialDataBuffer Put(byte value)
        {
            _stream.WriteByte(value);
            return this;
        }

        public SerialDataBuffer Put(byte[] source, int offset, int length)
        {
            _stream.Write(source, offset, length);
            return this;
        }

        public SerialDataBuffer Put(byte[] source, long offset, long length)
        {
            if (offset > Int32.MaxValue) { throw new ArgumentOutOfRangeException(nameof(offset)); }
            if (length > Int32.MaxValue) { throw new ArgumentOutOfRangeException(nameof(length)); }

            _stream.Write(source, (int)offset, (int)length);
            return this;
        }

        public SerialDataBuffer Put(byte[] source) => Put(source, 0, source.Length);

        public bool Equals(SerialDataBuffer compareBuffer)
        {
            bool result = false;

            if (compareBuffer != null && Remaining == compareBuffer.Remaining)
            {
                long thisOriginalPosition = Position;
                long compareOriginalPosition = compareBuffer.Position;

                bool differenceFound = false;
                while (_stream.Position < _stream.Length)
                {
                    if (GetNext() != compareBuffer.GetNext())
                    {
                        differenceFound = true;
                        break;
                    }
                }

                SetPosition(thisOriginalPosition);
                compareBuffer.SetPosition(compareOriginalPosition);

                result = !differenceFound;
            }

            return result;
        }

        #region BinaryReader methods

        public char GetChar() => _reader.ReadChar();

        public char GetChar(int index)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            char value = _reader.ReadChar();
            _stream.Position = originalPosition;
            return value;
        }

        public double GetDouble() => _reader.ReadDouble();

        public double GetDouble(int index)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            double value = _reader.ReadDouble();
            _stream.Position = originalPosition;
            return value;
        }

        public float GetFloat() => _reader.ReadSingle();

        public float GetFloat(int index)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            float value = _reader.ReadSingle();
            _stream.Position = originalPosition;
            return value;
        }

        public int GetInt() => _reader.ReadInt32();

        public int GetInt(int index)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            int value = _reader.ReadInt32();
            _stream.Position = originalPosition;
            return value;
        }

        public long GetLong() => _reader.ReadInt64();

        public long GetLong(int index)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            long value = _reader.ReadInt64();
            _stream.Position = originalPosition;
            return value;
        }

        public short GetShort() => _reader.ReadInt16();

        public short GetShort(int index)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            short value = _reader.ReadInt16();
            _stream.Position = originalPosition;
            return value;
        }

        #endregion

        #region BinaryWriter methods

        public SerialDataBuffer PutChar(char value)
        {
            _writer.Write(value);
            return this;
        }

        public SerialDataBuffer PutChar(int index, char value)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            _writer.Write(value);
            _stream.Position = originalPosition;
            return this;
        }

        public SerialDataBuffer PutDouble(double value)
        {
            _writer.Write(value);
            return this;
        }

        public SerialDataBuffer PutDouble(int index, double value)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            _writer.Write(value);
            _stream.Position = originalPosition;
            return this;
        }

        public SerialDataBuffer PutFloat(float value)
        {
            _writer.Write(value);
            return this;
        }

        public SerialDataBuffer PutFloat(int index, float value)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            _writer.Write(value);
            _stream.Position = originalPosition;
            return this;
        }

        public SerialDataBuffer PutInt(int value)
        {
            _writer.Write(value);
            return this;
        }

        public SerialDataBuffer PutInt(int index, int value)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            _writer.Write(value);
            _stream.Position = originalPosition;
            return this;
        }

        public SerialDataBuffer PutLong(long value)
        {
            _writer.Write(value);
            return this;
        }

        public SerialDataBuffer PutLong(int index, long value)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            _writer.Write(value);
            _stream.Position = originalPosition;
            return this;
        }

        public SerialDataBuffer PutShort(short value)
        {
            _writer.Write(value);
            return this;
        }

        public SerialDataBuffer PutShort(int index, short value)
        {
            long originalPosition = _stream.Position;
            _stream.Position = index;
            _writer.Write(value);
            _stream.Position = originalPosition;
            return this;
        }

        #endregion
    }
}