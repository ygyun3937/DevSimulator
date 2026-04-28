using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace MAK_EOL.Classes.PLC
{
    /// <summary>
    /// BasePLC 구현체 — SLMP(TCP) 프로토콜로 DevSimulator와 통신
    /// Mtbs_MXCom_PLC(ActUtlType COM) 대신 사용 가능
    /// </summary>
    public class SLMP_PLC : BasePLC
    {
        private TcpClient _tcp;
        private NetworkStream _stream;
        private readonly object _lock = new object();

        public SLMP_ConnectionData connectionData;

        #region // 생성자

        public SLMP_PLC(string ip, int port)
        {
            if (connectionData == null) connectionData = new SLMP_ConnectionData();
            connectionData.IP = ip;
            connectionData.Port = port;
        }

        public SLMP_PLC()
        {
            LoadPLCConnectionData();
        }

        #endregion

        #region // 연결 / 해제

        public override int DeviceOpen()
        {
            try
            {
                if (_tcp != null && _tcp.Connected)
                {
                    _iPLCConnect = true;
                    return 0; // Define._TRUE
                }

                _tcp = new TcpClient();
                _tcp.Connect(connectionData.IP, connectionData.Port);
                _tcp.ReceiveTimeout = 3000;
                _tcp.SendTimeout = 3000;
                _stream = _tcp.GetStream();
                _iPLCConnect = true;
                return 0;
            }
            catch (Exception)
            {
                _iPLCConnect = false;
                return -1; // Define._FALSE
            }
        }

        public override int DeviceClose()
        {
            try
            {
                _stream?.Close();
                _tcp?.Close();
                _stream = null;
                _tcp = null;
                _iPLCConnect = false;
                return 0;
            }
            catch
            {
                _iPLCConnect = false;
                return -1;
            }
        }

        #endregion

        #region // PLC Read 관련 method's

        public override int ReadDevice_Int(string sDeviceName, ref int iDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, sDeviceName);
                short val = SlmpReadWord(key);
                iDeviceValue = val;
                return 0;
            }
            catch { iDeviceValue = 0; return -1; }
        }

        public override int ReadDevice_Short(string sDeviceName, ref short iDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, sDeviceName);
                iDeviceValue = SlmpReadWord(key);
                return 0;
            }
            catch { iDeviceValue = 0; return -1; }
        }

        public override int ReadDeviceRandoms_Int(string[] DeviceNameRandom, ref int[] arrDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                arrDeviceValue = new int[DeviceNameRandom.Length];
                for (int i = 0; i < DeviceNameRandom.Length; i++)
                {
                    string key = string.Format("{0}{1}", Area, DeviceNameRandom[i]);
                    arrDeviceValue[i] = SlmpReadWord(key);
                }
                return 0;
            }
            catch { return -1; }
        }

        public override int ReadDeviceRandoms_Short(string[] DeviceNameRandom, ref short[] arrDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                arrDeviceValue = new short[DeviceNameRandom.Length];
                for (int i = 0; i < DeviceNameRandom.Length; i++)
                {
                    string key = string.Format("{0}{1}", Area, DeviceNameRandom[i]);
                    arrDeviceValue[i] = SlmpReadWord(key);
                }
                return 0;
            }
            catch { return -1; }
        }

        public override int ReadDeviceBlocks_Int(string szDeviceName, uint iDeviceSize, ref int[] arrDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, szDeviceName);
                short[] vals = SlmpReadWords(key, (ushort)iDeviceSize);
                arrDeviceValue = new int[vals.Length];
                for (int i = 0; i < vals.Length; i++)
                    arrDeviceValue[i] = vals[i];
                return 0;
            }
            catch { return -1; }
        }

        public override int ReadDeviceBlocks_Short(string szDeviceName, uint iDeviceSize, ref short[] arrDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, szDeviceName);
                arrDeviceValue = SlmpReadWords(key, (ushort)iDeviceSize);
                return 0;
            }
            catch { return -1; }
        }

        #endregion

        #region // PLC Write 관련 method's

        public override int WriteDevice_Int(string sDeviceName, int iDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, sDeviceName);
                SlmpWriteWord(key, (short)iDeviceValue);
                return 0;
            }
            catch { return -1; }
        }

        public override int WriteDevice_Short(string sDeviceName, short iDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, sDeviceName);
                SlmpWriteWord(key, iDeviceValue);
                return 0;
            }
            catch { return -1; }
        }

        public override int WriteDeviceRandoms_Int(string[] DeviceNameRandom, int[] arrDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                for (int i = 0; i < DeviceNameRandom.Length; i++)
                {
                    string key = string.Format("{0}{1}", Area, DeviceNameRandom[i]);
                    SlmpWriteWord(key, (short)arrDeviceValue[i]);
                }
                return 0;
            }
            catch { return -1; }
        }

        public override int WriteDeviceRandoms_Short(string[] DeviceNameRandom, short[] arrDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                for (int i = 0; i < DeviceNameRandom.Length; i++)
                {
                    string key = string.Format("{0}{1}", Area, DeviceNameRandom[i]);
                    SlmpWriteWord(key, arrDeviceValue[i]);
                }
                return 0;
            }
            catch { return -1; }
        }

        public override int WriteDeviceBlocks_Int(string szDeviceName, int[] arrDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, szDeviceName);
                short[] vals = new short[arrDeviceValue.Length];
                for (int i = 0; i < arrDeviceValue.Length; i++)
                    vals[i] = (short)arrDeviceValue[i];
                SlmpWriteWords(key, vals);
                return 0;
            }
            catch { return -1; }
        }

        public override int WriteDeviceBlocks_Short(string szDeviceName, short[] arrDeviceValue, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, szDeviceName);
                SlmpWriteWords(key, arrDeviceValue);
                return 0;
            }
            catch { return -1; }
        }

        #endregion

        #region // Bit 관련

        public override int Set_Bit(string sDeviceName, bool state, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, sDeviceName);
                SlmpWriteWord(key, state ? (short)1 : (short)0);
                return 0;
            }
            catch { return -1; }
        }

        public override int Get_Bit(string sDeviceName, ref bool state, string Area)
        {
            if (!_iPLCConnect) return -1;
            try
            {
                string key = string.Format("{0}{1}", Area, sDeviceName);
                short val = SlmpReadWord(key);
                state = val != 0;
                return 0;
            }
            catch { state = false; return -1; }
        }

        #endregion

        #region // Double Word (32bit)

        public override int GetDoubleDevice(string device, string Area)
        {
            if (!_iPLCConnect) return 0;
            try
            {
                string key = string.Format("{0}{1}", Area, device);
                short[] vals = SlmpReadWords(key, 2);
                return (vals[1] << 16) | (vals[0] & 0xFFFF);
            }
            catch { return 0; }
        }

        public override bool SetDoubleDevice(string address, int value, string Area)
        {
            if (!_iPLCConnect) return false;
            try
            {
                string key = string.Format("{0}{1}", Area, address);
                short low = (short)(value & 0xFFFF);
                short high = (short)((value >> 16) & 0xFFFF);
                SlmpWriteWords(key, new short[] { low, high });
                return true;
            }
            catch { return false; }
        }

        #endregion

        #region // Address 변환

        public override string GetBitAddressString(string DefineAddress)
        {
            return DefineAddress; // SLMP는 주소 그대로 사용
        }

        public override string GetWordAddressString(string DefineAddress)
        {
            return DefineAddress;
        }

        public override string GetByteAddressString(string DefineAddress)
        {
            return DefineAddress;
        }

        #endregion

        #region // 설정 저장/로드

        public override int LoadPLCConnectionData()
        {
            try
            {
                string path = string.Format(FileName, "SLMP");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    connectionData = JsonSerializer.Deserialize<SLMP_ConnectionData>(json);
                }
                else
                {
                    connectionData = new SLMP_ConnectionData();
                }
                return 0;
            }
            catch
            {
                connectionData = new SLMP_ConnectionData();
                return -1;
            }
        }

        public override int SavePLCConnectionData()
        {
            try
            {
                string path = string.Format(FileName, "SLMP");
                string json = JsonSerializer.Serialize(connectionData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return 0;
            }
            catch { return -1; }
        }

        #endregion

        #region // SLMP 프로토콜 통신 (private)

        private (char code, int no) ParseDevice(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 2)
                throw new ArgumentException("Invalid device key: " + key);
            return (char.ToUpper(key[0]), int.Parse(key.Substring(1)));
        }

        /// <summary>단일 워드 읽기</summary>
        private short SlmpReadWord(string deviceKey)
        {
            var vals = SlmpReadWords(deviceKey, 1);
            return vals[0];
        }

        /// <summary>연속 워드 블록 읽기</summary>
        private short[] SlmpReadWords(string deviceKey, ushort points)
        {
            var (code, no) = ParseDevice(deviceKey);

            // Request data: CPU timer(2) + Command(2) + Subcmd(2) + DeviceNo(3) + DeviceCode(1) + Points(2) = 12 bytes
            var data = new List<byte>
            {
                0x10, 0x00,                                          // CPU Monitor Timer
                0x01, 0x04,                                          // Command: READ (0x0401)
                0x00, 0x00,                                          // Subcommand
                (byte)(no & 0xFF), (byte)((no >> 8) & 0xFF), (byte)((no >> 16) & 0xFF),
                (byte)code,
                (byte)(points & 0xFF), (byte)((points >> 8) & 0xFF),
            };

            byte[] resp = SendReceive(BuildFrame(data));

            // 응답: header(9) + endCode(2) + data(points*2)
            ushort endCode = (ushort)(resp[9] | (resp[10] << 8));
            if (endCode != 0)
                throw new Exception(string.Format("SLMP Read error: 0x{0:X4}", endCode));

            short[] result = new short[points];
            for (int i = 0; i < points; i++)
                result[i] = (short)(resp[11 + i * 2] | (resp[12 + i * 2] << 8));
            return result;
        }

        /// <summary>단일 워드 쓰기</summary>
        private void SlmpWriteWord(string deviceKey, short value)
        {
            SlmpWriteWords(deviceKey, new short[] { value });
        }

        /// <summary>연속 워드 블록 쓰기</summary>
        private void SlmpWriteWords(string deviceKey, short[] values)
        {
            var (code, no) = ParseDevice(deviceKey);

            var data = new List<byte>
            {
                0x10, 0x00,                                          // CPU Monitor Timer
                0x01, 0x14,                                          // Command: WRITE (0x1401)
                0x00, 0x00,                                          // Subcommand
                (byte)(no & 0xFF), (byte)((no >> 8) & 0xFF), (byte)((no >> 16) & 0xFF),
                (byte)code,
                (byte)(values.Length & 0xFF), (byte)((values.Length >> 8) & 0xFF),
            };

            foreach (var v in values)
            {
                data.Add((byte)(v & 0xFF));
                data.Add((byte)((v >> 8) & 0xFF));
            }

            byte[] resp = SendReceive(BuildFrame(data));

            ushort endCode = (ushort)(resp[9] | (resp[10] << 8));
            if (endCode != 0)
                throw new Exception(string.Format("SLMP Write error: 0x{0:X4}", endCode));
        }

        private byte[] BuildFrame(List<byte> data)
        {
            ushort dataLen = (ushort)data.Count;
            var frame = new List<byte>
            {
                0x50, 0x00,                                          // Subheader
                0x00,                                                // Network No
                0xFF,                                                // PC No
                0xFF, 0x03,                                          // I/O No
                0x00,                                                // Station No
                (byte)(dataLen & 0xFF), (byte)((dataLen >> 8) & 0xFF),
            };
            frame.AddRange(data);
            return frame.ToArray();
        }

        private byte[] SendReceive(byte[] request)
        {
            lock (_lock)
            {
                if (_stream == null) throw new InvalidOperationException("Not connected");
                _stream.Write(request, 0, request.Length);
                var buffer = new byte[4096];
                int read = _stream.Read(buffer, 0, buffer.Length);
                if (read == 0) throw new Exception("Connection closed");
                var result = new byte[read];
                Array.Copy(buffer, result, read);
                return result;
            }
        }

        #endregion

        #region // 연결 데이터

        public class SLMP_ConnectionData
        {
            public string IP { get; set; } = "127.0.0.1";
            public int Port { get; set; } = 5000;
        }

        #endregion
    }
}
