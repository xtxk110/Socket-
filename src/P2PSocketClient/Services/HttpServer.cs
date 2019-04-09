﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wireboy.Socket.P2PClient.Models;

namespace Wireboy.Socket.P2PClient.Services
{
    public class HttpServer
    {
        Dictionary<string, TcpClient> m_httpClientMap = new Dictionary<string, TcpClient>();
        P2PService _p2PService;
        TaskFactory _taskFactory = new TaskFactory();
        public HttpServer(P2PService p2PService)
        {
            _p2PService = p2PService;
        }
        public void Start()
        {
            if (!string.IsNullOrEmpty(ConfigServer.AppSettings.HttpServerName))
            {
                byte[] bytes = GetHttpSendBytes(HttpMsgType.Http服务名, new byte[1], ConfigServer.AppSettings.HttpServerName.ToBytes());
                _p2PService.ServerTcp.WriteAsync(bytes, MsgType.Http服务);
                Console.WriteLine("{0}Http服务-设置http服务名:{1}", _p2PService.m_curLogTime, ConfigServer.AppSettings.HttpServerName);
            }
        }
        public void RecieveServerTcp(byte[] data)
        {
            try
            {
                int index = 0;
                byte type = data.ReadByte(ref index);
                short length = data.ReadShort(ref index);
                byte[] curGuid = data.ReadBytes(ref index, length);
                string guidKey = curGuid.ToStringUnicode();

                switch (type)
                {
                    case (byte)HttpMsgType.Http数据:
                        {
                            length = data.ReadShort(ref index);
                            byte[] bytes = data.ReadBytes(ref index, length);
                            if (!m_httpClientMap.ContainsKey(guidKey))
                            {
                                string domain = GetHttpRequestHost(bytes, bytes.Length);
                                ConnectWebServer(curGuid, domain);
                            }
                            if (m_httpClientMap.ContainsKey(guidKey))
                            {
                                m_httpClientMap[guidKey].WriteAsync(bytes, MsgType.不封包);
                            }

                        }
                        break;
                    case (byte)HttpMsgType.断开连接:
                        {
                            m_httpClientMap.Remove(guidKey);
                        }
                        break;
                    case (byte)HttpMsgType.None:
                        {

                        }
                        break;
                }
            }
            catch (Exception ex)
            {

            }
        }
        public void ConnectWebServer(byte[] curGuid,string domain)
        {
            string key_in = curGuid.ToStringUnicode();
            if (!m_httpClientMap.ContainsKey(key_in))
            {
                HttpModel model = MatchHttpModel(domain);
                if (model == null) return;
                //连接网站
                TcpClient tcpClient = new TcpClient("127.0.0.1", model.WebPort);
                m_httpClientMap.Add(key_in, tcpClient);
                _taskFactory.StartNew(() =>
                {
                    string key = key_in;
                    try
                    {
                        TcpClient client = tcpClient;
                        NetworkStream stream = client.GetStream();
                        byte[] webBytes = new byte[1024];
                        while (true)
                        {
                            if (!m_httpClientMap.ContainsKey(key))
                            {
                                client.Close();
                                break;
                            }
                            int webLength = stream.Read(webBytes, 0, webBytes.Length);
                            if (webLength > 0)
                            {
                                byte[] webRet = GetHttpSendBytes(HttpMsgType.Http数据, curGuid, webBytes.Take(webLength).ToArray());
                                //发送数据
                                _p2PService.ServerTcp.WriteAsync(webRet, MsgType.Http服务);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        m_httpClientMap.Remove(key);
                    }
                });
            }
        }
        /// <summary>
        /// 获取请求的域名（如果返回空，则说明不是http请求，可能是https）
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public string GetHttpRequestHost(byte[] bytes, int length)
        {
            bool read = false;
            List<byte> byteList = new List<byte>();
            for (int i = 0; i < length; i++)
            {
                if (bytes[i] == 13 && (i + 1) < length && bytes[i + 1] == 10)
                {
                    if (read) break;
                    i++;
                    read = true;
                }
                else if (read)
                {
                    byteList.Add(bytes[i]);
                }
            }
            String str = Encoding.ASCII.GetString(byteList.ToArray());
            int indexOf = str.IndexOf(':');
            if (indexOf > -1) str = str.Substring(indexOf + 1).Trim();
            return str;
        }
        public HttpModel MatchHttpModel(string domain)
        {
            string webDomain = domain.Split(':').FirstOrDefault();
            return ConfigServer.HttpSettings.Where(t => {
                if (webDomain.StartsWith(t.Domain))
                    return true;
                return false;
            }).FirstOrDefault();
        }
        public byte[] GetHttpSendBytes(HttpMsgType httpMsgType, byte[] guid, byte[] data)
        {
            List<byte> ret = new List<byte>();
            ret.Add((byte)httpMsgType);
            ret.AddRange(guid.TransferSendBytes());
            ret.AddRange(data.TransferSendBytes());
            return ret.ToArray();
        }
    }
}
