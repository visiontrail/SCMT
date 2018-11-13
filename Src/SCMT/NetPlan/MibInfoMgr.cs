﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonUtility;
using DataBaseUtil;
using LinkPath;
using LmtbSnmp;
using LogManager;
using MIBDataParser;
using NetPlan.DevLink;
using SCMTOperationCore.Control;
using SCMTOperationCore.Elements;
using MAP_DEVTYPE_DEVATTRI = System.Collections.Generic.Dictionary<NetPlan.EnumDevType, System.Collections.Generic.List<NetPlan.DevAttributeInfo>>;

namespace NetPlan
{
	// MIB信息管理类，单例
	public class MibInfoMgr : Singleton<MibInfoMgr>
	{
		private delegate string GetMibValue(string strOriginValue, string strLatestValue);

		#region 公共接口

		/// <summary>
		/// 查询所有enb中获取的网规信息
		/// 调用时机：打开网规页面初始化成功后
		/// </summary>
		/// <returns></returns>
		public MAP_DEVTYPE_DEVATTRI GetAllEnbInfo()
		{
			lock (_syncObj)
			{
				return m_mapAllMibData;
			}
		}

		/// <summary>
		/// 获取一个指定类型和索引的设备信息，里面保存了所有的设备属性
		/// </summary>
		/// <param name="strIndex"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public DevAttributeInfo GetDevAttributeInfo(string strIndex, EnumDevType type)
		{
			if (string.IsNullOrEmpty(strIndex) || EnumDevType.unknown == type)
			{
				return null;
			}

			lock (_syncObj)
			{
				if (m_mapAllMibData.ContainsKey(type))
				{
					var devList = m_mapAllMibData[type];
					var dev = GetSameIndexDev(devList, strIndex);
					if (null != dev)
					{
						return dev;
					}
				}
			}

			NPLastErrorHelper.SetLastError($"未找到索引为{strIndex}的{type.ToString()}设备");
			return null;
		}

		/// <summary>
		/// 根据设备类型、索引、属性名查找该属性的值
		/// </summary>
		/// <param name="strIndex"></param>
		/// <param name="type"></param>
		/// <param name="strAttriName"></param>
		/// <returns></returns>
		public string GetDevAttributeValue(string strIndex, EnumDevType type, string strAttriName)
		{
			if (string.IsNullOrEmpty(strAttriName))
			{
				throw new CustomException($"传入参数无效");
			}

			var devWithSameIndex = GetDevAttributeInfo(strIndex, type);
			if (null == devWithSameIndex)
			{
				Log.Error($"未找到索引为 {strIndex} 类型为 {type.ToString()} 的设备");
				return null;
			}

			var mapAttri = devWithSameIndex.m_mapAttributes;
			if (null == mapAttri || 0 == mapAttri.Count)
			{
				Log.Error($"索引为 {strIndex} 类型为 {type.ToString()} 的设备属性信息为空");
				return null;
			}

			var strAttriValue = GetEnumStringByMibName(mapAttri, strAttriName);
			if (null == strAttriValue)
			{
				Log.Error($"索引为 {strIndex} 类型为 {type.ToString()} 的设备属性 {strAttriName} 值为null");
				return null;
			}

			return strAttriValue;
		}

		public string GetDevAttributeValue(DevAttributeInfo dev, string strAttriName)
		{
			if (null == dev || string.IsNullOrEmpty(strAttriName))
			{
				throw new CustomException("传入参数无效");
			}

			var strAttriValue = GetEnumStringByMibName(dev.m_mapAttributes, strAttriName);
			return strAttriValue;
		}

		/// <summary>
		/// 解析连接
		/// </summary>
		/// <returns></returns>
		public bool ParseLinks()
		{
			lock (_syncObj)
			{
				return m_linkMgr.ParseLinksFromMibInfo(m_mapAllMibData);
			}
		}

		/// 获取所有的连接信息
		public Dictionary<EnumDevType, List<WholeLink>> GetLinks()
		{
			return m_linkMgr.GetAllLink();
		}

		/// <summary>
		/// 保存一类设备的所有属性
		/// </summary>
		/// <param name="type"></param>
		/// <param name="listDevInfo"></param>
		public void AddDevMibInfo(EnumDevType type, List<DevAttributeInfo> listDevInfo)
		{
			if (null == listDevInfo || listDevInfo.Count == 0)
			{
				return;
			}

			lock (_syncObj)
			{
				if (m_mapAllMibData.ContainsKey(type))
				{
					m_mapAllMibData[type].AddRange(listDevInfo);	// 已经存在同类的设备信息，直接添加
				}
				else
				{
					m_mapAllMibData[type] = listDevInfo;			// 还不存在同类的设备信息，直接保存
				}
			}
		}

		/// <summary>
		/// 增加一个设备属性
		/// </summary>
		/// <param name="type"></param>
		/// <param name="devInfo"></param>
		public void AddDevMibInfo(EnumDevType type, DevAttributeInfo devInfo)
		{
			if (null == devInfo)
			{
				return;
			}

			lock (_syncObj)
			{
				if (m_mapAllMibData.ContainsKey(type))
				{
					m_mapAllMibData[type].Add(devInfo);
				}
				else
				{
					var listDev = new List<DevAttributeInfo> {devInfo};
					m_mapAllMibData[type] = listDev;
				}
			}
		}

		/// <summary>
		/// 增加一个新的天线设备
		/// </summary>
		/// <param name="nIndex">设备序号</param>
		/// <param name="type"></param>
		/// <returns>null:添加失败</returns>
		public DevAttributeInfo AddNewAnt(int nIndex, EnumDevType type = EnumDevType.ant)
		{
			var ant = GerenalNewDev(type, nIndex);
			if (null == ant)
			{
				return null;
			}

			if (!MoveDevFromWaitDelToModifyMap(type, ant, ant.m_strOidIndex))
			{
				return null;
			}

			return ant;
		}

		/// <summary>
		/// 新增一个板卡
		/// </summary>
		/// <param name="slot">插槽号</param>
		/// <param name="strBoardType">板卡类型</param>
		/// <param name="strWorkMode">工作模式</param>
		/// <param name="strIrFrameType">帧结构</param>
		/// <returns></returns>
		public DevAttributeInfo AddNewBoard(int slot, string strBoardType, string strWorkMode, string strIrFrameType)
		{
			if (string.IsNullOrEmpty(strBoardType) || string.IsNullOrEmpty(strWorkMode) ||
				string.IsNullOrEmpty(strIrFrameType))
			{
				throw new ArgumentNullException();
			}

			const EnumDevType type = EnumDevType.board;
			var dev = GerenalNewDev(type, slot);
			if (null == dev)
			{
				Log.Error($"生成新设备属性失败，可能已经存在相同索引相同类型的设备");
				return null;
			}

			if (!dev.SetFieldOriginValue("netBoardType", strBoardType) ||
			    !dev.SetFieldOriginValue("netBoardWorkMode", strWorkMode) ||
			    !dev.SetFieldOriginValue("netBoardIrFrameType", strIrFrameType))
			{
				Log.Error("设置新板卡属性失败");
				NPLastErrorHelper.SetLastError("设备新板卡属性失败");
				return null;
			}

			if (!MoveDevFromWaitDelToModifyMap(type, dev, dev.m_strOidIndex))
			{
				return null;
			}

			return dev;
		}

		/// <summary>
		/// 增加新的RRU设备
		/// </summary>
		/// <param name="seqIndexList">RRU编号列表</param>
		/// <param name="nRruType">RRU设备类型索引</param>
		/// <param name="strWorkMode">工作模式</param>
		/// <returns></returns>
		public List<DevAttributeInfo> AddNewRru(List<int> seqIndexList, int nRruType, string strWorkMode)
		{
			if (null == seqIndexList || string.IsNullOrEmpty(strWorkMode))
			{
				Log.Error("传入RRU索引列表为null；工作模式为null或空");
				NPLastErrorHelper.SetLastError($"新增RRU失败，传入RRU索引列表或工作模式为空");
				return null;
			}

			var type = EnumDevType.rru;
			var rruList = new List<DevAttributeInfo>();
			foreach (var seqIndex in seqIndexList)
			{
				var newRru = GerenalNewDev(type, seqIndex);
				if (null == newRru)
				{
					Log.Error($"根据RRU编号{seqIndex}生成新设备失败");
					return null;
				}

				if (!newRru.SetFieldOriginValue("netRRUTypeIndex", nRruType.ToString()) ||
					!newRru.SetFieldOriginValue("netRRUOfpWorkMode", strWorkMode))
				{
					Log.Error("设置RRU参数netRRUTypeIndex、netRRUOfpWorkMode失败");
					NPLastErrorHelper.SetLastError($"设置RRU工作模式失败");
					return null;
				}

				if (!MoveDevFromWaitDelToModifyMap(type, newRru, newRru.m_strOidIndex))
				{
					return null;
				}

				Log.Debug($"编号为{seqIndex}的RRU设备添加成功");
				rruList.Add(newRru);
			}

			return rruList;
		}

		/// <summary>
		/// 增加新的RHUB设备
		/// 1.5G当前版本rhub的4个光口要全部连接到基带板，否则小区无法建立
		/// 2.rhub光口编号1~4,eth口编号1~8
		/// 3.前期5G不支持rhub级联
		/// 4.rhub的4个光口对应hbpod板卡的2、3、4、5光口，顺序无要求，底层写死
		/// 5.只支持正常模式
		/// 6.上联口编号1，2，连接接入板；下联口编号3，4，级联使用
		/// </summary>
		/// <param name="seqIndexList">要添加设备的索引列表</param>
		/// <param name="strDevVer">设备版本。rhub分1.0和2.0两个版本，用于UI绘图</param>
		/// <param name="strWorkMode"></param>
		/// <returns>null:添加rhub设备失败；</returns>
		public List<RHubDevAttri> AddNewRhub(List<int> seqIndexList, string strDevVer, string strWorkMode)
		{
			if (null == seqIndexList || string.IsNullOrEmpty(strWorkMode) ||
				string.IsNullOrEmpty(strDevVer))
			{
				Log.Error("传入rhub索引列表为null；工作模式为null或空");
				return null;
			}

			var type = EnumDevType.rhub;
			var rhubList = new List<RHubDevAttri>();
			foreach (var seqIndex in seqIndexList)
			{
				var dev = new RHubDevAttri(seqIndex, strDevVer);
				if (dev.m_mapAttributes.Count == 0)
				{
					Log.Error($"编号为{seqIndex}的rhub设备属性数量为0");
					return null;
				}

				var devIndex = dev.m_strOidIndex;
				if (HasSameIndexDev(m_mapAllMibData, type, devIndex))
				{
					Log.Error($"已经存在编号为{seqIndex}的rhub设备，添加失败");
					return null;
				}
				dev.m_recordType = RecordDataType.NewAdd;

				if (!dev.SetFieldOriginValue("netRHUBOfpWorkMode", strWorkMode))
				{
					Log.Error("设置rhub参数netRHUBOfpWorkMode失败");
					return null;
				}

				if (!dev.SetFieldOriginValue("netRHUBType", strDevVer))
				{
					Log.Error("设置rhub参数netRHUBType失败");
					return null;
				}

				if (!MoveDevFromWaitDelToModifyMap(type, dev, devIndex))
				{
					return null;
				}

				Log.Debug($"编号为{seqIndex}的rhub设备添加成功");
				rhubList.Add(dev);
			}
			return rhubList;
		}

		/// <summary>
		/// 新增本地小区
		/// </summary>
		/// <param name="nLocalCellId"></param>
		/// <returns></returns>
		public DevAttributeInfo AddNewLocalCell(int nLocalCellId)
		{
			const EnumDevType type = EnumDevType.nrNetLc;
			var dev = GerenalNewDev(type, nLocalCellId);
			if (null == dev)
			{
				Log.Error($"生成本地小区{nLocalCellId}的属性失败");
				return null;
			}

			return !MoveDevFromWaitDelToModifyMap(type, dev, dev.m_strOidIndex) ? null : dev;
		}

		/// <summary>
		/// 增加本地小区布配开关类型的信息
		/// </summary>
		/// <param name="strIndex"></param>
		/// <param name="strSwitchValue"></param>
		/// <returns></returns>
		public DevAttributeInfo AddNewNetLcCtrlSwitch(string strIndex, string strSwitchValue)
		{
			var type = EnumDevType.nrNetLcCtr;
			var dev = new DevAttributeInfo(type, strIndex);

			if (!dev.SetFieldOriginValue("nrNetLocalCellCtrlConfigSwitch", strSwitchValue))
			{
				Log.Error($"设置字段 nrNetLocalCellCtrlConfigSwitch 的 OriginValue 为{strSwitchValue}失败");
				return null;
			}

			dev.m_recordType = RecordDataType.Original;
			lock (_syncObj)
			{
				AddDevToMap(m_mapAllMibData, type, dev);
			}
			return dev;
		}

		/// <summary>
		/// 增加连接
		/// </summary>
		/// <param name="srcEndpoint"></param>
		/// <param name="dstEndpoint"></param>
		/// <returns></returns>
		public bool AddLink(LinkEndpoint srcEndpoint, LinkEndpoint dstEndpoint)
		{
			var linkType = EnumDevType.unknown;
			if (!GetLinkTypeBySrcDstEnd(srcEndpoint, dstEndpoint, ref linkType))
			{
				Log.Error("获取连接类型失败");
				return false;
			}

			var wlink = new WholeLink(srcEndpoint, dstEndpoint);

			var handler = LinkFactory.CreateLinkHandler(linkType);
			if (null == handler)
			{
				Log.Error($"连接类型{linkType.ToString()}尚未提供支持");
				return false;
			}

			lock (_syncObj)
			{
				return handler.AddLink(wlink, ref m_mapAllMibData);
			}
		}

		/// <summary>
		/// 把类型为devType的Dev添加到给定的map中
		/// </summary>
		/// <param name="mapData"></param>
		/// <param name="devType"></param>
		/// <param name="dev"></param>
		public static bool AddDevToMap(MAP_DEVTYPE_DEVATTRI mapData, EnumDevType devType, DevAttributeInfo dev)
		{
			if (null == mapData || devType == EnumDevType.unknown || null == dev)
			{
				return false;
			}

			if (mapData.ContainsKey(devType))
			{
				mapData[devType].Add(dev);
			}
			else
			{
				var devList = new List<DevAttributeInfo> { dev };
				mapData[devType] = devList;
			}

			return true;
		}

		/// <summary>
		/// 删除类型为devType，索引为strIndex的设备
		/// </summary>
		/// <param name="strIndex"></param>
		/// <param name="devType"></param>
		public bool DelDev(string strIndex, EnumDevType devType)
		{
			if (string.IsNullOrEmpty(strIndex) || EnumDevType.unknown == devType)
			{
				Log.Error($"传入设备索引{strIndex}或类型{devType.ToString()}错误");
				return false;
			}

			var dev = GetDevAttributeInfo(strIndex, devType);
			if (null == dev)
			{
				Log.Error($"未找到类型为{devType.ToString()}索引为{strIndex}的记录");
				return false;
			}

			lock (_syncObj)
			{
				DelDevFromMap(m_mapAllMibData, devType, dev);
			}
			return true;
		}

		/// <summary>
		/// 删除连接
		/// </summary>
		/// <param name="srcEndpoint"></param>
		/// <param name="dstEndpoint"></param>
		/// <returns></returns>
		public bool DelLink(LinkEndpoint srcEndpoint, LinkEndpoint dstEndpoint)
		{
			var linkType = EnumDevType.unknown;
			if (!GetLinkTypeBySrcDstEnd(srcEndpoint, dstEndpoint, ref linkType))
			{
				Log.Error("获取连接类型失败，删除连接失败");
				return false;
			}

			var wlink = new WholeLink(srcEndpoint, dstEndpoint);
			var handler = LinkFactory.CreateLinkHandler(linkType);
			if (null == handler)
			{
				Log.Error($"尚未支持类型为{linkType.ToString()}的连接处理");
				return false;
			}

			lock (_syncObj)
			{
				return handler.DelLink(wlink, ref m_mapAllMibData);
			}
		}

		/// <summary>
		/// 直接从内存中删除符合条件的
		/// </summary>
		/// <param name="strIndex"></param>
		/// <param name="devType"></param>
		/// <returns></returns>
		public bool DelDevFromMemory(string strIndex, EnumDevType devType)
		{
			if (string.IsNullOrEmpty(strIndex) || EnumDevType.unknown == devType)
			{
				throw new CustomException($"传入设备索引{strIndex}或类型{devType.ToString()}错误");
			}

			var dev = GetDevAttributeInfo(strIndex, devType);
			if (null == dev)
			{
				Log.Error($"未找到类型为{devType.ToString()}索引为{strIndex}的记录");
				return false;
			}

			lock (_syncObj)
			{
				DelDevFromMap(m_mapAllMibData, devType, dev, true);
			}
			return true;
		}

		/// <summary>
		/// 设置指定设备的属性值
		/// </summary>
		/// <param name="strIndex">设备索引号</param>
		/// <param name="strFieldName">字段英文名</param>
		/// <param name="strValue">字段值</param>
		/// <param name="devType">设备类型</param>
		/// <returns></returns>
		public bool SetDevAttributeValue(string strIndex, string strFieldName, string strValue, EnumDevType devType)
		{
			if (string.IsNullOrEmpty(strIndex) || EnumDevType.unknown == devType)
			{
				return false;
			}

			lock (_syncObj)
			{
				// 从基站中查询到设备修改属性，就需要移动到modify队列，等待参数下发
				if (!m_mapAllMibData.ContainsKey(devType)) return false;

				var devList = m_mapAllMibData[devType];
				var dev = GetSameIndexDev(devList, strIndex);
				if (null == dev) return false;

				dev.SetFieldLatestValue(strFieldName, strValue);
				if (RecordDataType.NewAdd != dev.m_recordType)
				{
					dev.m_recordType = RecordDataType.Modified;
				}
				return true;
			}
		}

		/// <summary>
		/// 下发板卡信息到基站
		/// </summary>
		/// <returns></returns>
		public bool DistributeNetPlanInfoToEnb(EnumDevType devType)
		{
			var targetIp = CSEnbHelper.GetCurEnbAddr();
			if (null == targetIp)
			{
				throw new CustomException("下发网规参数失败，尚未选中基站");
			}

			lock (_syncObj)
			{
				if (!m_mapAllMibData.ContainsKey(devType) || m_mapAllMibData[devType].Count <= 0) return true;

				var mibList = m_mapAllMibData[devType];
				var waitRmList = new List<DevAttributeInfo>();

				foreach (var item in mibList)
				{
					if (RecordDataType.Original == item.m_recordType)
					{
						continue;
					}

					var cmdType = EnumSnmpCmdType.Invalid;
					if (RecordDataType.NewAdd == item.m_recordType)
					{
						cmdType = EnumSnmpCmdType.Add;
					}
					else if (RecordDataType.Modified == item.m_recordType)
					{
						cmdType = EnumSnmpCmdType.Set;
					}
					else if (RecordDataType.WaitDel == item.m_recordType)
					{
						cmdType = EnumSnmpCmdType.Del;
					}

					if (!DistributeSnmpData(item, cmdType, targetIp))
					{
						var log = $"类型为{devType.ToString()}，索引为{item.m_strOidIndex}的网规信息下发{cmdType.ToString()}失败";
						Log.Error(log);
						NPLastErrorHelper.SetLastError(log);
						return false;
					}

					if (EnumSnmpCmdType.Del == cmdType)
					{
						waitRmList.Add(item);		// 如果是要删除的设备，参数下发后，直接删除内存中的数据
					}
					else
					{
						item.m_recordType = RecordDataType.Original;    // 下发成功的都设置为原始数据
					}

					Log.Debug($"类型为{devType.ToString()}，索引为{item.m_strOidIndex}的网规信息下发{cmdType.ToString()}成功");

					if (EnumDevType.nrNetLc == devType && !NPCellOperator.SetNetPlanSwitch(false, item.m_strOidIndex, targetIp))
					{
						Log.Error($"关闭本地小区{item.m_strOidIndex}布配开关失败");
						NPLastErrorHelper.SetLastError($"关闭本地小区{item.m_strOidIndex.Trim('.')}布配开关失败");
						return false; // TODO 此处返回，已经下发的删除的本地小区网规信息存在不一致的问题
					}
				}

				foreach (var wrmDev in waitRmList)
				{
					mibList.Remove(wrmDev);
				}
			}

			return true;
		}

		/// <summary>
		/// 根据rru设备的索引获取该RRU每个通道上配置的本地小区配置信息
		/// </summary>
		/// <param name="strIndex"></param>
		public Dictionary<string, NPRruToCellInfo> GetNetLcInfoByRruIndex(string strIndex)
		{
			var targetIp = CSEnbHelper.GetCurEnbAddr();
			if (null == targetIp)
			{
				throw new CustomException("尚未选中基站");
			}

			var retMap = new Dictionary<string, NPRruToCellInfo>();
			var devType = EnumDevType.rru_ant;
			var rruNo = int.Parse(strIndex.Trim('.'));
			lock (_syncObj)
			{
				if (m_mapAllMibData.ContainsKey(devType))
				{
					var devList = m_mapAllMibData[devType];
					foreach (var item in devList)
					{
						GetRruPortToCellInfo(item, rruNo, ref retMap, (RecordDataType.Original == item.m_recordType));
					}
				}
			}

			return retMap;
		}

		/// <summary>
		/// 配置RRU通道与本地小区的关联关系
		/// </summary>
		/// <returns></returns>
		public bool SetNetLcInfo(string strRruIndex, Dictionary<string, NPRruToCellInfo> portToCellInfoList)
		{
			var targetIp = CSEnbHelper.GetCurEnbAddr();
			if (null == targetIp)
			{
				throw new CustomException("尚未选中基站");
			}

			var devType = EnumDevType.rru_ant;
			var rruNo = strRruIndex.Trim('.');
			var waitRemoveList = new List<string>();

			lock (_syncObj)
			{
				if (m_mapAllMibData.ContainsKey(devType))
				{
					var devList = m_mapAllMibData[devType];
					foreach (var kv in portToCellInfoList) // 遍历传入的所有值
					{
						var strIndex = $".{rruNo}.{kv.Key}"; // 用rru编号和port编号组成索引
						var dev = GetSameIndexDev(devList, strIndex);
						if (null != dev)
						{
							var mapAttri = dev.m_mapAttributes;

							SetRruAntSettingTableInfo(mapAttri, kv.Value);
							if (dev.m_recordType != RecordDataType.NewAdd)
							{
								dev.m_recordType = RecordDataType.Modified;
							}
						}
					}
				}
			}

			// 如果还有信息没有用完，就new一个rru_ant对象，存入m_mapNewAdd
			if (portToCellInfoList.Count > 0)
			{
				foreach (var item in portToCellInfoList)
				{
					var newDev = AddNewRruAntDev(rruNo, item.Key, item.Value);
					if (null == newDev)
					{
						Log.Error("新加天线阵安装规划表实例失败");
						return false;
					}
					lock (_syncObj)
					{
						AddDevToMap(m_mapAllMibData, EnumDevType.rru_ant, newDev);
					}
				}
			}

			return true;
		}

		/// <summary>
		/// 增加一行天线阵安装规划表记录
		/// </summary>
		/// <param name="strRruNo"></param>
		/// <param name="strPort"></param>
		/// <param name="lcInfo"></param>
		/// <returns></returns>
		public DevAttributeInfo AddNewRruAntDev(string strRruNo, string strPort, NPRruToCellInfo lcInfo)
		{
			var strIndex = $".{strRruNo.Trim('.')}.{strPort}";
			var newDev = new DevAttributeInfo(EnumDevType.rru_ant, strIndex);
			if (!SetRruAntSettingTableInfo(newDev.m_mapAttributes, lcInfo))
			{
				return null;
			}
			newDev.m_recordType = RecordDataType.NewAdd;
			return newDev;
		}

		/// <summary>
		/// 根据本地小区ID从天线安装规划表中查询对应的行
		/// </summary>
		/// <param name="nLcId"></param>
		/// <returns></returns>
		public List<DevAttributeInfo> GetRowFromRruAntSetTableByLcId(int nLcId)
		{
			if (0 < nLcId || nLcId > 35)	// TODO 小区的数量先写死为36个
			{
				return null;
			}

			var retList = new List<DevAttributeInfo>();
			var strLcId = nLcId.ToString();
			var devType = EnumDevType.rru_ant;
			lock (_syncObj)
			{
				if (!m_mapAllMibData.ContainsKey(devType)) return retList;

				var devList = m_mapAllMibData[devType];
				retList.AddRange(
					from dai in devList
					let lcId1 = GetEnumStringByMibName(dai.m_mapAttributes, "netSetRRUPortSubtoLocalCellId")
					let lcId2 = GetEnumStringByMibName(dai.m_mapAttributes, "netSetRRUPortSubtoLocalCellId2")
					let lcId3 = GetEnumStringByMibName(dai.m_mapAttributes, "netSetRRUPortSubtoLocalCellId3")
					let lcId4 = GetEnumStringByMibName(dai.m_mapAttributes, "netSetRRUPortSubtoLocalCellId4")
					where strLcId == lcId4 || strLcId == lcId1 || strLcId == lcId3 || strLcId == lcId2
					select dai
					);
			}

			return retList;
		}

		/// <summary>
		/// 根据本地小区ID在天线阵安装规划表中找到CellId相同的信息，把CellId对应的属性设置为-1
		/// </summary>
		/// <param name="nLcId"></param>
		/// <returns></returns>
		public bool ResetRelateLcIdInNetRruAntSettingTblByLcId(int nLcId)
		{
			var devType = EnumDevType.rru_ant;
			var strLcId = nLcId.ToString();
			lock (_syncObj)
			{
				if (!m_mapAllMibData.ContainsKey(devType))
				{
					Log.Error($"未找到类型为rru_ant的规划信息");
					return false;
				}

				var devList = m_mapAllMibData[devType];

				// 需要遍历所有的天线阵安装规划表
				Parallel.ForEach(devList, item =>
				{
					var mapAttributes = item.m_mapAttributes;

					for (var j = 1; j <= 4; j++)
					{
						var mibName = "netSetRRUPortSubtoLocalCellId";
						if (j > 1)
						{
							mibName += $"{j}";
						}

						if (!mapAttributes.ContainsKey(mibName))
						{
							Log.Error($"在天线阵安装规划表中没有找到名为{mibName}的节点");
							return;
						}

						var lcValue = GetEnumStringByMibName(mapAttributes, mibName);
						if (lcValue != strLcId) continue;

						mapAttributes[mibName].SetLatestValue("-1");

						if (RecordDataType.NewAdd != item.m_recordType)
						{
							item.m_recordType = RecordDataType.Modified;
						}
					}
				});

				//for (var i = 0; i < devList.Count; i++)
				//{
				//	var dev = devList.ElementAt(i);
				//	var mapAttributes = dev.m_mapAttributes;

				//	for (var j = 1; j <= 4; j++)
				//	{
				//		var mibName = "netSetRRUPortSubtoLocalCellId";
				//		if (j > 1)
				//		{
				//			mibName += $"{j}";
				//		}

				//		if (!mapAttributes.ContainsKey(mibName))
				//		{
				//			Log.Error($"在天线阵安装规划表中没有找到名为{mibName}的节点");
				//			return false;
				//		}

				//		var lcValue = GetEnumStringByMibName(mapAttributes, mibName);
				//		if (lcValue != strLcId) continue;

				//		mapAttributes[mibName].SetValue("-1");

				//		if (RecordDataType.NewAdd != dev.m_recordType)
				//		{
				//			dev.m_recordType = RecordDataType.Modified;
				//		}
				//	}
				//}
			}

			return true;
		}

		/// <summary>
		/// 清理所有的数据
		/// </summary>
		public void Clear()
		{
			lock (_syncObj)
			{
				m_mapAllMibData.Clear();
				m_linkMgr.Clear();
			}
		}

		/// <summary>
		/// 获取RHUB设备连接的板卡的插槽号。遍历4个光口
		/// </summary>
		public static string GetRhubLinkToBoardSlotNo(DevAttributeInfo rhub)
		{
			for (var i = 1; i < 5; i++)
			{
				var mibName = (i == 1) ? "netRHUBAccessSlotNo" : $"netRHUBOfp{i}SlotNo";
				var boardSlot = MibInfoMgr.GetNeedUpdateValue(rhub, mibName);
				if (null != boardSlot && "-1" != boardSlot)
				{
					return boardSlot;
				}
			}

			return "-1";
		}

		#endregion

		#region 私有接口

		private MibInfoMgr()
		{
			m_mapAllMibData = new MAP_DEVTYPE_DEVATTRI();
			m_linkMgr = new NetPlanLinkMgr();
		}

		/// <summary>
		/// 判断已修改、所有、新添加的设备列表中是否存在与给定的索引相同设备
		/// </summary>
		/// <param name="mapMibInfo"></param>
		/// <param name="devType"></param>
		/// <param name="strIndex"></param>
		/// <returns></returns>
		private static bool HasSameIndexDev(MAP_DEVTYPE_DEVATTRI mapMibInfo, EnumDevType devType, string strIndex)
		{
			if (!mapMibInfo.ContainsKey(devType)) return false;

			var devList = mapMibInfo[devType];
			return HasSameIndexDev(devList, strIndex);
		}

		// 判断给定的列表中是否存在与strIndex相同索引的设备
		private static bool HasSameIndexDev(List<DevAttributeInfo> devList, string strIndex)
		{
			return null != devList && devList.Any(dev => dev.m_strOidIndex == strIndex);
		}

		private DevAttributeInfo GetSameIndexDev(List<DevAttributeInfo> devList, string strIndex)
		{
			return devList.FirstOrDefault(dev => dev.m_strOidIndex == strIndex);
		}

		/// <summary>
		/// 从map中删除数据
		/// </summary>
		/// <param name="mapData">待操作的map</param>
		/// <param name="devType">设备类型</param>
		/// <param name="dai">设备属性</param>
		/// <param name="bDelReal">是否立即删除，不仅是设置记录类型</param>
		public static void DelDevFromMap(MAP_DEVTYPE_DEVATTRI mapData, EnumDevType devType, DevAttributeInfo dai, bool bDelReal = false)
		{
			if (null == mapData || null == dai)
			{
				return;
			}

			if (!mapData.ContainsKey(devType))
			{
				return;
			}

			if (dai.m_recordType == RecordDataType.NewAdd || bDelReal)
			{
				mapData[devType].Remove(dai);
			}
			else
			{
				dai.m_recordType = RecordDataType.WaitDel;
			}
		}

		/// <summary>
		/// 根据devIndex在waitDel队列中查找，是否存在相同索引相同类型的设备，如果存在就从waitDel中删除，把新生成的设备加入到modify队列中
		/// </summary>
		/// <param name="type"></param>
		/// <param name="newDev"></param>
		/// <param name="devIndex"></param>
		/// <returns></returns>
		private bool MoveDevFromWaitDelToModifyMap(EnumDevType type, DevAttributeInfo newDev, string devIndex)
		{
			// 新加的设备，要判断索引和待删除列表中的是否一致
			// 目的：防止先删除所有的同类设备，然后重新添加，但是所有的属性设置的又相同的情况
			lock (_syncObj)
			{
				if (!m_mapAllMibData.ContainsKey(type))
				{
					AddDevToMap(m_mapAllMibData, type, newDev);
					return true;
				}

				var devList = m_mapAllMibData[type];
				foreach (var dev in devList)
				{
					// 如果在待删除的列表中，就直接把新创建的设备放到已修改的列表中
					if (dev.m_strOidIndex != devIndex) continue;

					if (RecordDataType.WaitDel == dev.m_recordType)
					{
						devList.Remove(dev);

						// 需要比对dev和newDev，把dev的值和newDev的信息合并到一起，并修改设备record类型为modify
						newDev.AdjustOtherDevOriginValueToMyOrigin(dev);
						newDev.m_recordType = RecordDataType.Modified;
						AddDevToMap(m_mapAllMibData, type, newDev);
					}

					return true;
				}

				// 流程走到这里，肯定是在devList中没有找到索引相同的设备
				AddDevToMap(m_mapAllMibData, type, newDev);
			}

			return true;
		}

		/// <summary>
		/// 生成一个指定类型和索引最后一级值的设备
		/// </summary>
		/// <param name="type"></param>
		/// <param name="lastIndexValue"></param>
		/// <returns></returns>
		private DevAttributeInfo GerenalNewDev(EnumDevType type, int lastIndexValue)
		{
			var dev = new DevAttributeInfo(type, lastIndexValue);
			if (dev.m_mapAttributes.Count == 0)
			{
				Log.Error($"创建类型为{type.ToString()}最后一个索引值为{lastIndexValue}的新dev失败");
				return null;
			}

			var devIndex = dev.m_strOidIndex;

			List<DevAttributeInfo> devList = null;
			lock (_syncObj)
			{
				if (m_mapAllMibData.ContainsKey(type))
				{
					devList = m_mapAllMibData[type];
				}
			}

			if (null != devList)
			{
				var oldDev = GetSameIndexDev(devList, devIndex);
				if (null != oldDev && RecordDataType.WaitDel != oldDev.m_recordType)
				{
					Log.Error($"存在相同类型{type.ToString()}相同索引{devIndex}的设备");
					NPLastErrorHelper.SetLastError($"已存在编号为{lastIndexValue}的{type.ToString()}设备");
					return null;
				}
			}

			dev.m_recordType = RecordDataType.NewAdd;

			return dev;
		}

		/// <summary>
		/// 根据listColumns中的每个元素，在devInfo中找到对应的值，组装成字典，用于下发
		/// </summary>
		/// <param name="devInfo"></param>
		/// <param name="listColumns"></param>
		/// <param name="gmv"></param>
		/// <param name="strRs">行状态的值：4，6</param>
		/// <returns></returns>
		private static Dictionary<string, string> GeneralName2ValueMap(DevAttributeInfo devInfo, List<MibLeaf> listColumns, GetMibValue gmv, string strRs = "4")
		{
			if (null == devInfo || null == listColumns)
			{
				return null;
			}

			var n2v = new Dictionary<string, string>();
			var absMap = devInfo.m_mapAttributes;

			foreach (var leafInfo in listColumns)
			{
				var leafName = leafInfo.childNameMib;

				// 行状态的值特殊处理
				if (leafInfo.ASNType.Equals("RowStatus", StringComparison.OrdinalIgnoreCase))
				{
					n2v.Add(leafName, strRs);
				}
				else
				{
					if (!absMap.ContainsKey(leafName))
					{
						continue;
					}

					var mi = absMap[leafName];
					var value = gmv?.Invoke(mi.m_strOriginValue, mi.m_strLatestValue);
					if (null == value)
					{
						continue;
					}

					// value 有可能是枚举值等描述信息，需要翻转为snmp类型
					var ret = SnmpToDatabase.ConvertStringToMibValue(leafInfo, value);
					n2v.Add(leafName, ret);
				}
			}

			return n2v;
		}

		/// <summary>
		/// 获取最新值
		/// </summary>
		/// <param name="strOriginValue"></param>
		/// <param name="strLatestValue"></param>
		/// <returns></returns>
		private string GetLatestValue(string strOriginValue, string strLatestValue)
		{
			if (string.IsNullOrEmpty(strOriginValue))
			{
				return null;
			}

			var value = strOriginValue;

			if (null != strLatestValue)
			{
				value = strLatestValue;
			}
			return value;
		}

		/// <summary>
		/// 获取需要更新的值
		/// </summary>
		/// <param name="strOriginValue"></param>
		/// <param name="strLatestValue"></param>
		/// <returns></returns>
		private static string GetNeedUpdateValue(string strOriginValue, string strLatestValue)
		{
			if (string.IsNullOrEmpty(strOriginValue))
			{
				return null;
			}

			if (null == strLatestValue || strLatestValue == strOriginValue)
			{
				return strOriginValue;
			}

			return strLatestValue;
		}

		public static string GetNeedUpdateValue(DevAttributeInfo dev, string strFieldName, bool bConvert = true)
		{
			if (null == dev || string.IsNullOrEmpty(strFieldName))
			{
				throw new ArgumentNullException(strFieldName);
			}

			var mapAttributes = dev.m_mapAttributes;
			if (!mapAttributes.ContainsKey(strFieldName))
			{
				Log.Error($"索引为{dev.m_strOidIndex}的设备属性中不包含{strFieldName}字段");
				return null;
			}

			var originValue = dev.GetFieldOriginValue(strFieldName, bConvert);
			var latestValue = dev.GetFieldLatestValue(strFieldName, bConvert);

			return GetNeedUpdateValue(originValue, latestValue);
		}

		/// <summary>
		/// 布配网规信息
		/// </summary>
		/// <param name="devAttribute"></param>
		/// <param name="cmdType"></param>
		/// <param name="targetIp"></param>
		/// <returns></returns>
		private bool DistributeSnmpData(DevAttributeInfo devAttribute, EnumSnmpCmdType cmdType, string targetIp)
		{
			if (string.IsNullOrEmpty(targetIp))
			{
				throw new CustomException("下发网规信息功能传入目标IP参数错误");
			}

			if (EnumSnmpCmdType.Invalid == cmdType)
			{
				throw new CustomException("下发网规信息功能传入SNMP命令类型错误");
			}

			//var enbType = NodeBControl.GetInstance().GetEnbTypeByIp(targetIp);
			var enbType = EnbTypeEnum.ENB_EMB6116;
			var cmdList = NPECmdHelper.GetInstance().GetCmdList(devAttribute.m_enumDevType, cmdType, enbType);
			if (null == cmdList || 0 == cmdList.Count)
			{
				throw new CustomException($"未找到类型为{devAttribute.m_enumDevType.ToString()}的{cmdType.ToString()}相关命令");
			}

			var cmdToMibLeafMap = NPECmdHelper.GetInstance().GetSameTypeCmdMibLeaf(cmdList);
			if (null == cmdToMibLeafMap || 0 == cmdToMibLeafMap.Count)
			{
				throw new CustomException($"未找到类型为{devAttribute.m_enumDevType.ToString()}的{cmdType.ToString()}相关命令详细信息");
			}

			var strRs = "4";
			GetMibValue gmv = GetLatestValue;
			switch (cmdType)
			{
				case EnumSnmpCmdType.Set:
					gmv = GetNeedUpdateValue;
					break;
				case EnumSnmpCmdType.Add:
					break;
				case EnumSnmpCmdType.Del:
					strRs = "6";
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(cmdType), cmdType, null);
			}

			foreach (var kv in cmdToMibLeafMap)
			{
				var cmdName = kv.Key;
				var mibLeafList = kv.Value;

				var name2Value = GeneralName2ValueMap(devAttribute, mibLeafList, gmv, strRs);
				var ret = CDTCmdExecuteMgr.CmdSetSync(cmdName, name2Value, devAttribute.m_strOidIndex, targetIp);
				if (0 != ret)
				{
					if (2 == ret)
					{
						var desc = SnmpErrDescHelper.GetLastErrorDesc();
						Log.Error($"下发命令{cmdName}失败，原因：{desc}");
					}
					else
					{
						Log.Error($"下发命令{cmdName}失败");
					}

					return false;       // TODO 一个设备信息下发失败是要结束整个过程吗？
				}
			}

			return true;
		}

		/// <summary>
		/// 从DevAttributeInfo的m_mapAttributes数据结构中取出strMibName对应的值
		/// 如果是枚举值就转换为字符串
		/// </summary>
		/// <param name="mapInfos"></param>
		/// <param name="strMibName"></param>
		/// <returns></returns>
		private string GetEnumStringByMibName(Dictionary<string, MibLeafNodeInfo> mapInfos, string strMibName)
		{
			if (null == mapInfos || string.IsNullOrEmpty(strMibName))
			{
				return null;
			}

			if (!mapInfos.ContainsKey(strMibName))
			{
				return null;
			}

			var attri = mapInfos[strMibName];
			if (null == attri)
			{
				return null;
			}

			var ret = (null == attri.m_strLatestValue)
				? SnmpToDatabase.ConvertValueToString(attri.mibAttri, attri.m_strOriginValue)
				: SnmpToDatabase.ConvertValueToString(attri.mibAttri, attri.m_strLatestValue);

			return ret;
		}

		/// <summary>
		/// 获取RRU通道对应的小区信息
		/// </summary>
		/// <param name="dai"></param>
		/// <param name="nRruNo"></param>
		/// <param name="mapResult"></param>
		/// <param name="bCellFix"></param>
		private bool GetRruPortToCellInfo(DevAttributeInfo dai, int nRruNo, ref Dictionary<string, NPRruToCellInfo> mapResult, bool bCellFix = false)
		{
			var mapAttri = dai.m_mapAttributes;
			if (!mapAttri.ContainsKey("netSetRRUNo"))
				return false;

			var mibl = mapAttri["netSetRRUNo"];
			var setNo = int.Parse(mibl.m_strLatestValue);   // modify队列中肯定都是修改的
			if (nRruNo != setNo)
				return false;

			var trxStatus = GetEnumStringByMibName(mapAttri, "netSetRRUPortTxRxStatus");

			// 通道频道信息是根据小区的id从netLc表中找到netLcFreqBand字段的值
			// RRU 的ID相同，取出所有通道对应的信息
			var rtc = new NPRruToCellInfo(trxStatus);

			var sb = new StringBuilder();
			for (var i = 2; i <= 4; i++)
			{
				var mibName = $"netSetRRUPortSubtoLocalCellId{i}";
				var cellId = GetEnumStringByMibName(mapAttri, mibName);
				if (null != cellId && "-1" != cellId)
				{
					var freqBand = GetValueFromNetLcTableByLcIdAndMibName(int.Parse(cellId), "nrNetLocalCellFreqBand");
					if (!string.IsNullOrEmpty(freqBand))
					{
						sb.AppendFormat(freqBand + "/");
					}

					rtc.CellIdList.Add(new CellAndState {cellId = cellId, bIsFixed = bCellFix});
				}
			}

			var cellId1 = GetEnumStringByMibName(mapAttri, "netSetRRUPortSubtoLocalCellId");
			if (null != cellId1 && -1 != int.Parse(cellId1))
			{
				var freqBand = GetValueFromNetLcTableByLcIdAndMibName(int.Parse(cellId1), "nrNetLocalCellFreqBand");
				if (!string.IsNullOrEmpty(freqBand))
				{
					sb.AppendFormat(freqBand + "/");
				}
				rtc.CellIdList.Add(new CellAndState { cellId = cellId1, bIsFixed = bCellFix });
			}

			var portNo = GetEnumStringByMibName(mapAttri, "netSetRRUPortNo");
			if (null == portNo)
			{
				return false;
			}
			rtc.FreqBand = sb.ToString();
			mapResult.Add(portNo, rtc);

			return true;
		}

		/// <summary>
		/// 根据本地小区ID和MIB节点名从netLocalCellTable表中查找对应的值
		/// </summary>
		/// <param name="nLcId"></param>
		/// <param name="strMibName">mib名称</param>
		/// <returns></returns>
		private string GetValueFromNetLcTableByLcIdAndMibName(int nLcId, string strMibName)
		{
			if (0 < nLcId || nLcId > 35)
			{
				Log.Error("传入的本地小区ID超出[0,35]范围");
				return null;
			}

			var devType = EnumDevType.nrNetLc;

			lock (_syncObj)
			{
				if (m_mapAllMibData.ContainsKey(devType))
				{
					var devList = m_mapAllMibData[devType];
					var dev = GetSameIndexDev(devList, $".{nLcId}");
					if (null != dev)
					{
						return GetEnumStringByMibName(dev.m_mapAttributes, strMibName);
					}
				}
			}

			return null;
		}

		/// <summary>
		/// 设置天线阵安装规划表的信息。修改时使用
		/// </summary>
		/// <param name="mapAttributes"></param>
		/// <param name="lcInfo"></param>
		/// <returns></returns>
		private bool SetRruAntSettingTableInfo(Dictionary<string, MibLeafNodeInfo> mapAttributes, NPRruToCellInfo lcInfo)
		{
			if (mapAttributes.ContainsKey("netSetRRUPortTxRxStatus"))
			{
				var tempAtrri = mapAttributes["netSetRRUPortTxRxStatus"];
				tempAtrri.SetLatestValue(lcInfo.TxRxStatus);
			}

			var lcIdList = lcInfo.CellIdList;
			var lcAttr1 = mapAttributes["netSetRRUPortSubtoLocalCellId"];
			var lcAttr2 = mapAttributes["netSetRRUPortSubtoLocalCellId2"];
			var lcAttr3 = mapAttributes["netSetRRUPortSubtoLocalCellId3"];
			var lcAttr4 = mapAttributes["netSetRRUPortSubtoLocalCellId4"];

			// 先设置为-1。可能会删除通道关联的小区
			lcAttr1.SetLatestValue("-1");
			lcAttr2.SetLatestValue("-1");
			lcAttr3.SetLatestValue("-1");
			lcAttr4.SetLatestValue("-1");
			for (int i = 1; i <= lcIdList.Count; i++)
			{
				if (1 == i)
				{
					lcAttr1.SetLatestValue(lcIdList[i - 1].ToString());
				}
				if (2 == i)
				{
					lcAttr2.SetLatestValue(lcIdList[i - 1].ToString());
				}
				if (3 == i)
				{
					lcAttr3.SetLatestValue(lcIdList[i - 1].ToString());
				}
				if (4 == i)
				{
					lcAttr4.SetLatestValue(lcIdList[i - 1].ToString());
					break;
				}
			}

			return true;
		}

		/// <summary>
		/// 根据连接两端信息，获取连接的类型
		/// </summary>
		/// <param name="srcEndpoint">源端信息</param>
		/// <param name="dstEndpoint">目的端信息</param>
		/// <param name="linkType">ref 连接类型，传出参数</param>
		/// <returns>true:源和目的信息有效，获取到有效类型；false:其他情况</returns>
		private static bool GetLinkTypeBySrcDstEnd(LinkEndpoint srcEndpoint, LinkEndpoint dstEndpoint, ref EnumDevType linkType)
		{
			var link = new WholeLink(srcEndpoint, dstEndpoint);
			linkType = link.GetLinkType();
			return (linkType != EnumDevType.unknown);
		}

		// 设置设备指定字段值
		public static bool SetDevAttributeValue(DevAttributeInfo dev, string strFieldName, string strValue)
		{
			if (null == dev || string.IsNullOrEmpty(strFieldName) || string.IsNullOrEmpty(strValue))
			{
				throw new CustomException("属性值传入参数有误");
			}

			if (!dev.m_mapAttributes.ContainsKey(strFieldName))
			{
				Log.Error($"索引为{dev.m_strOidIndex}的设备中未找到{strFieldName}字段，无法设置字段值");
				return false;
			}

			//dev.m_mapAttributes[strFieldName].SetLatestValue(strValue);
			dev.SetFieldLatestValue(strFieldName, strValue);
			if (dev.m_recordType != RecordDataType.NewAdd)
			{
				dev.m_recordType = RecordDataType.Modified;
			}

			return true;
		}

		#endregion


		#region 私有成员、数据

		// 保存所有的网规MIB数据。开始保存的是从设备中查询回来的信息，如果对这些信息进行了修改、删除，就从这个数据结构中移动到对应的结构中
		// 最后下发网规信息时，就不再下发这个数据结构中的信息
		private MAP_DEVTYPE_DEVATTRI m_mapAllMibData;

		private readonly NetPlanLinkMgr m_linkMgr;

		private readonly object _syncObj = new object();

		#endregion
	}
}
