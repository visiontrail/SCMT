﻿using System;
using System.Timers;
using AtpMessage.MsgDefine;
using CommonUility;
using MsgQueue;
using Timer = System.Timers.Timer;

namespace AtpMessage.LinkMgr
{
	public abstract class NetElementLinkBase : INetElementLink
	{
		public enum LinkState
		{
			Connecting,
			Connencted,
			Disconnected,
		}

		public LinkState State { get; protected set; } = LinkState.Disconnected;

		public bool IsBreak => LinkState.Disconnected == State;

		/// <summary>
		/// 连接网元。此处仅考虑ATP的连接
		/// </summary>
		/// <param name="netElementAddress">网元配置信息</param>
		/// <param name="isRepeatConnect">是否重复连接</param>
		public void Connect(NetElementConfig netElementAddress, bool isRepeatConnect = true)
		{
			if (null == netElementAddress)
			{
				throw new ArgumentNullException("netElementAddress is null");
			}

			//如果已经连接或者连接中，就不再处理
			if (IsBreak)
			{
				Logon(netElementAddress);
			}
		}

		public void Disconnect()
		{
			Logoff(_netElementConfig);
		}

		public bool IsConnected()
		{
			return LinkState.Connencted == State;
		}

		/// <summary>
		/// 登录板卡。分为直连板卡登录和非直连板卡登录
		/// 直连板卡很简单，只要构造报文即可
		/// 非直连还需要通知建链等操作
		/// </summary>
		public virtual void Logon(NetElementConfig netElementAddress)
		{
			State = LinkState.Connecting;
			_netElementConfig = netElementAddress;
		}

		/// <summary>
		/// 断开ATP的连接。虚函数，直连和非直连操作不一样
		/// </summary>
		public virtual void Logoff(NetElementConfig netElementAddress)
		{
			State = LinkState.Disconnected;

			//取消保活定时器
			_kaTimer?.Stop();
			_kaTimer?.Dispose();
		}

		/// <summary>
		/// 登录结果。有两种结果：收到报文，登录成功；超时或者其他情况，登录失败
		/// 登录成功后，启动定时器，发送保活报文
		/// </summary>
		public void OnLogonResult(bool bSucceed)
		{
			if (bSucceed)
			{
				State = LinkState.Connencted;

				//启动任务发送保活报文,2分钟发送一次，gtsa的超时时间为5分钟
				_kaTimer = new Timer(2000) { AutoReset = true };
				_kaTimer.Elapsed += SendKeepAlivePacket;
				_kaTimer.Start();
			}
			else
			{
				State = LinkState.Disconnected;
			}
		}

		/// <summary>
		/// 发送保活报文。
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void SendKeepAlivePacket(object sender, ElapsedEventArgs e)
		{
			if (null == _kaBytes)
			{
				MsgGtsm2GtsaAliveRpt kaReq = new MsgGtsm2GtsaAliveRpt()
				{
					header =
					{
						u8RemoteMode = GtsMsgType.CONNECT_DIRECT_MSG,
						u16SourceID = GtsMsgType.DEST_GTSM,
						u16Opcode = GtsMsgType.O_GTSMGTSA_ALIVE_RPT,
						u16DestID = _netElementConfig.Index
					}
				};
				kaReq.header.u16Length = kaReq.ContentLen;
				_kaBytes = SerializeHelper.SerializeStructToBytes(kaReq);
			}

			SendPackets(_kaBytes);
		}

		/// <summary>
		/// 发送报文。就是调用了PublishHelper
		/// </summary>
		/// <param name="dataByteses">要发送的数据流</param>
		/// <returns>发送的字节数</returns>
		public int SendPackets(byte[] dataByteses)
		{
			PublishHelper.PublishMsg("topic", dataByteses);     //TODO TOPIC
			return dataByteses.Length;
		}

		private NetElementConfig _netElementConfig;
		private Timer _kaTimer;             //发送保活报文定时器
		private byte[] _kaBytes;            //保活报文，避免每次重新构造
	}
}