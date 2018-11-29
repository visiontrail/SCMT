﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using CommonUtility;
using DataBaseUtil;
using LinkPath;
using LmtbSnmp;
using LogManager;
using MIBDataParser;
using MIBDataParser.JSONDataMgr;
using SCMTMainWindow.Component.ViewModel;
using SCMTMainWindow.Utils;

namespace SCMTMainWindow.View
{
	/// <summary>
	/// 基本信息右键菜单参数设置
	/// </summary>
	public partial class MainDataParaSetGrid : Window
	{
		/// <summary>
		/// 保存当前命令的属性节点信息
		/// </summary>
		private CmdMibInfo cmdMibInfo = new CmdMibInfo();

		/// <summary>
		/// 保存索引节点信息
		/// </summary>
		private List<MibLeaf> listIndexInfo = new List<MibLeaf>();

		private MibTable m_MibTable;

		/// <summary>
		/// 0为查询指令，1为增加，2为删除，3为修改指令
		/// </summary>
		private int m_operType = 0;
        private int m_ModifyIndexGrade = 0;

        //private bool m_bAllSelect = true;

		public bool bOK = false;

		/// <summary>
		/// 动态表的所有列信息,该属性必须设置，否则无法正常显示;
		/// 设置该属性之后，动态表就会将所有列对应的模板全部生成;
		/// </summary>
		private DyDataGrid_MIBModel m_ParaModel;

		private MainDataGrid m_MainDataGrid;

		public MainDataParaSetGrid(MainDataGrid dataGrid)
		{
			InitializeComponent();

			m_MainDataGrid = dataGrid;

			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			this.DynamicParaSetGrid.SelectionChanged += DynamicParaSetGrid_SelectionChanged;
			this.DynamicParaSetGrid.BeginningEdit += DynamicParaSetGrid_BeginningEdit;
			this.DynamicParaSetGrid.LostFocus += DynamicParaSetGrid_LostFocus;
		}

		private void DynamicParaSetGrid_LostFocus(object sender, RoutedEventArgs e)
		{
			if (typeof(TextBox) != e.OriginalSource.GetType())
			{
				return;
			}
			string cellValue = "";

			DataGrid dataGrid = (DataGrid)sender;

			if (!(dataGrid.CurrentCell.Item is DyDataGrid_MIBModel))
			{
				return;
			}
			// 行Model
			DyDataGrid_MIBModel mibModel = (DyDataGrid_MIBModel)dataGrid.CurrentCell.Item;

			// TextBox
			if (typeof(TextBox) == e.OriginalSource.GetType())
			{
				cellValue = (e.OriginalSource as TextBox).Text;
				//用于处理参数值列(目前是第2列)单元格内容为字符串时，修改后对列表的显示
				if (mibModel.PropertyList[1].Item3 is DataGrid_Cell_MIB)
				{
					var ff = mibModel.PropertyList[1].Item3 as DataGrid_Cell_MIB;
					if (!string.IsNullOrWhiteSpace(cellValue))
						ff.m_Content = cellValue;
				}
			}
		}

		private void DynamicParaSetGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
		{
			dynamic temp = e.Column.GetCellContent(e.Row).DataContext as DyDataGrid_MIBModel;
			// 根据不同的列（既数据类型）改变不同的处理策略;
			try
			{
				temp.JudgeParaPropertyName_StartEditing(e.Column.Header);
			}
			catch (Exception ex)
			{
			}
		}

		private void DynamicParaSetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// 如果SelectedIndex是-1，则表明是初始化过程中调用的;
			if (((e.OriginalSource as ComboBox).SelectedIndex == -1))
			{
				return;
			}
			else
			{
				try
				{
					(sender as DataGrid).SelectedCells[0].Item.GetType();
				}
				catch (Exception ex)
				{
					return;
				}
			}
			try
			{
				((sender as DataGrid).SelectedCells[0].Item as DyDataGrid_MIBModel).JudgePropertyName_ChangeSelection(
					(sender as DataGrid).SelectedCells[0].Column.Header.ToString(), (e.OriginalSource as ComboBox).SelectedItem);
			}
			catch
			{
			}
		}

		private void BtnOK_Click(object sender, RoutedEventArgs e)
		{
			string strMsg;
			bOK = true;
			//获取右键菜单列表内容
			ObservableCollection<DyDataGrid_MIBModel> datalist = new ObservableCollection<DyDataGrid_MIBModel>();
			datalist = (ObservableCollection<DyDataGrid_MIBModel>)this.DynamicParaSetGrid.DataContext;

            if (m_operType != 0)//添加修改
            {
                //将右键菜单列表内容转换成与基本信息列表格式相同结构
                dynamic model = new DyDataGrid_MIBModel();
                string value;
                string strPreOid = SnmpToDatabase.GetMibPrefix();
                // 索引
                string strIndex = "";
                string strFullOid = "";
                var indexGrade = 0;
                foreach (DyDataGrid_MIBModel mm in datalist)
                {
                    var cell = mm.Properties["ParaValue"] as GridCell;
                    if (cell.cellDataType == LmtbSnmp.DataGrid_CellDataType.enumType)
                    {
                        var emnuCell = cell as DataGrid_Cell_MIB_ENUM;
                        if (emnuCell != null)
                            value = emnuCell.m_CurrentValue.ToString();
                        else
                            value = cell.m_Content;
                    }
                    else
                        value = cell.m_Content;

                    // 获取Mib节点属性
                    MibLeaf mibLeaf = SnmpToDatabase.GetMibNodeInfoByName(cell.MibName_EN, CSEnbHelper.GetCurEnbAddr());
                    if (null == mibLeaf)
                    {
                        strMsg = "无法获取Mib节点信息！";
                        Log.Error(strMsg);
                        MessageBox.Show(strMsg);
                        return;
                    }
                    // 获取索引节点
                    if ("True".Equals(mibLeaf.IsIndex) && m_operType == 1) // 只有添加时才获取索引
                    {
                        strIndex += "." + value;
                        indexGrade++;
                        continue;
                    }

                    if (!cell.oid.Contains(strPreOid))
                    {
                        strFullOid = strPreOid + cell.oid + strIndex;
                    }
                    else
                    {
                        strFullOid = cell.oid + strIndex;
                    }

                    var dgm = DataGridCellFactory.CreateGridCell(cell.MibName_EN, cell.MibName_CN, value, strFullOid, CSEnbHelper.GetCurEnbAddr());

                    model.AddProperty(cell.MibName_EN, dgm, cell.MibName_CN);
                }

                // 像基站下发添加指令
                // 行数据
                Dictionary<string, object> lineData = ((DyDataGrid_MIBModel)model).Properties;
                // Mib英文名称与值的对应关系
                Dictionary<string, string> enName2Value = new Dictionary<string, string>();
                // 根据DataGrid行数据组装Mib英文名称与值的对应关系
                if (false == DataGridUtils.MakeEnName2Value(lineData, ref enName2Value))
                {
                    strMsg = "DataGridUtils.MakeEnName2Value()方法执行错误！";
                    Log.Error(strMsg);
                    MessageBox.Show("添加参数失败！");
                    return;
                }

                // 组装Vb列表
                List<CDTLmtbVb> setVbs = new List<CDTLmtbVb>();
                if (false == DataGridUtils.MakeSnmpVbs(lineData, enName2Value, ref setVbs, out strMsg))
                {
                    Log.Error(strMsg);
                    return;
                }

                // SNMP Set
                long requestId;
                CDTLmtbPdu lmtPdu = new CDTLmtbPdu();
                // 发送SNMP Set命令
                int res = CDTCmdExecuteMgr.VbsSetSync(setVbs, out requestId, CSEnbHelper.GetCurEnbAddr(), ref lmtPdu, true);
                if (res != 0)
                {
                    strMsg = string.Format("参数配置失败，EnbIP:{0}", CSEnbHelper.GetCurEnbAddr());
                    Log.Error(strMsg);
                    MessageBox.Show(strMsg);
                    return;
                }
                // 判读SNMP响应结果
                if (lmtPdu.m_LastErrorStatus != 0)
                {
                    strMsg = $"参数配置失败，错误信息:{SnmpErrDescHelper.GetErrDescById(lmtPdu.m_LastErrorStatus)}";
                    Log.Error(strMsg);
                    MessageBox.Show(strMsg);
                    return;
                }

                if (m_operType == 3)
                {
                    MessageBox.Show("参数修改成功！");
                }
                else if (m_operType == 1)
                {
                    MessageBox.Show("参数添加成功！");
                }

                //下发指令成功后更新基本信息列表
                if (m_operType == 3)
                    indexGrade = m_ModifyIndexGrade;
                m_MainDataGrid.RefreshDataGrid(lmtPdu, indexGrade);
            }
            else//查询
            {
                string value;
                string strindex = "";
                string indexDes = "";
                Dictionary<string, string> dicMibToValue = new Dictionary<string, string>();
                Dictionary<string, string> dicMibToOid = new Dictionary<string, string>();
                Dictionary<int, string> dicNoToValue = new Dictionary<int, string>();
                Dictionary<int, string> dicNoToDes = new Dictionary<int, string>();

                m_MainDataGrid.GetChildMibInfo(cmdMibInfo, ref dicMibToValue, ref dicMibToOid);

                //获取查询的索引信息，进行组合
                foreach (DyDataGrid_MIBModel mm in datalist)
                {
                    var cell = mm.Properties["ParaValue"] as GridCell;
                    if (cell.cellDataType == LmtbSnmp.DataGrid_CellDataType.enumType)
                    {
                        var emnuCell = cell as DataGrid_Cell_MIB_ENUM;
                        if (emnuCell != null)
                            value = emnuCell.m_CurrentValue.ToString();
                        else
                            value = cell.m_Content;
                    }
                    else
                        value = cell.m_Content;

                    // 获取Mib节点属性
                    MibLeaf mibLeaf = SnmpToDatabase.GetMibNodeInfoByName(cell.MibName_EN, CSEnbHelper.GetCurEnbAddr());
                    if (null == mibLeaf)
                    {
                        return;
                    }
                    dicNoToValue.Add(mibLeaf.childNo, value);
                    dicNoToDes.Add(mibLeaf.childNo, mibLeaf.childNameCh);
                }

                List<int> q = (from d in dicNoToValue orderby d.Key select d.Key).ToList();//根据索引号排序

                foreach (int key in q)
                {
                    strindex += "." + dicNoToValue[key];
                    indexDes += dicNoToDes[key] + dicNoToValue[key];
                }

                //下发查询指令
                if (CommLinkPath.GetMibValueFromCmdExeResult(strindex, cmdMibInfo.m_cmdNameEn, ref dicMibToValue, CSEnbHelper.GetCurEnbAddr()))
                {
                    m_MainDataGrid.QuerySuccessRefreshDataGrid(dicMibToValue, dicMibToOid, q.Count, indexDes);
                }
            }

			this.Close();
		}

		private void BtnCancle_Click(object sender, RoutedEventArgs e)
		{
			bOK = false;
			this.Close();
		}

		private void CheckSelect_Click(object sender, RoutedEventArgs e)
		{
		}

		public void InitAddParaSetGrid(CmdMibInfo mibInfo, MibTable table, int operType)
		{
			cmdMibInfo = mibInfo;
            m_operType = operType;
			m_MibTable = table;

			listIndexInfo.Clear();
			foreach (MibLeaf leaf in table.childList)
			{
				if (leaf.IsIndex.Equals("True"))
					listIndexInfo.Add(leaf);
			}

			this.Title = mibInfo.m_cmdDesc;
			int i = 0;
			ObservableCollection<DyDataGrid_MIBModel> datalist = new ObservableCollection<DyDataGrid_MIBModel>();
			if (cmdMibInfo == null)
				return;

			if (cmdMibInfo.m_cmdDesc.Equals(this.Title))
			{
				if (listIndexInfo.Count > 0)
				{
					//索引节点
					foreach (MibLeaf mibLeaf in listIndexInfo)
					{
						dynamic model = new DyDataGrid_MIBModel();
                        model.AddParaProperty("ParaName", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.childNameCh,
							oid = mibLeaf.childOid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "参数名称");

						model.AddParaProperty("ParaValue", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.defaultValue,
							oid = mibLeaf.childOid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "参数值");

						model.AddParaProperty("ParaValueRange", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.managerValueRange,
							oid = mibLeaf.childOid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "取值范围");

						model.AddParaProperty("Unit", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.unit,
							oid = mibLeaf.childOid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "单位");

						// 将这个整行数据填入List;
						if (model.Properties.Count != 0)
						{
							// 向单元格内添加内容;
							datalist.Add(model);
							i++;
						}
						// 最终全部收集完成后，为控件赋值;
						if (i == datalist.Count)
						{
							this.ParaDataModel = model;
							this.DynamicParaSetGrid.DataContext = datalist;
						}
					}
				}

				if (cmdMibInfo.m_leaflist.Count > 0)
				{
					//属性节点
					foreach (string oid in cmdMibInfo.m_leaflist)
					{
						MibLeaf mibLeaf = Database.GetInstance().GetMibDataByOid(oid, CSEnbHelper.GetCurEnbAddr());
						dynamic model = new DyDataGrid_MIBModel();

						string devalue = ConvertValidValue(mibLeaf);
						//对行状态默认值为无效行时，改变为有效
						if (mibLeaf.childNameCh.Contains("行状态"))
						{
							var mapKv = MibStringHelper.SplitManageValue(mibLeaf.managerValueRange);

							if (devalue.Equals("6"))
							{
								devalue = "4";
							}
						}

                        model.AddParaProperty("ParaName", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.childNameCh,
							oid = mibLeaf.childOid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "参数名称");

						// 在这里要区分DataGrid要显示的数据类型;
						var dgm = DataGridCellFactory.CreateGridCell(mibLeaf.childNameMib, mibLeaf.childNameCh, devalue, mibLeaf.childOid, CSEnbHelper.GetCurEnbAddr());

						model.AddParaProperty("ParaValue", dgm, "参数值");

						model.AddParaProperty("ParaValueRange", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.managerValueRange,
							oid = mibLeaf.childOid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "取值范围");

						model.AddParaProperty("ParaUnit", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.unit,
							oid = mibLeaf.childOid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "单位");

						// 将这个整行数据填入List;
						if (model.Properties.Count != 0)
						{
							// 向单元格内添加内容;
							datalist.Add(model);
							i++;
						}

						// 最终全部收集完成后，为控件赋值;
						if (i == datalist.Count)
						{
							this.ParaDataModel = model;
							this.DynamicParaSetGrid.DataContext = datalist;
						}
					}
				}
			}
		}

		/// <summary>
		/// 根据基本信息列表选择的行填充信息，对于填充第一条数据信息(后续添加)
		/// </summary>
		/// <param name="model"></param>
		public bool InitModifyParaSetGrid(CmdMibInfo mibInfo, DyDataGrid_MIBModel mibModel, MibTable table, int operType)
		{
			if (mibModel == null || mibInfo == null)
				return false;

			cmdMibInfo = mibInfo;
			m_MibTable = table;
            m_operType = operType;
            this.Title = mibInfo.m_cmdDesc;
			listIndexInfo.Clear();
            m_ModifyIndexGrade = 0;

            foreach (MibLeaf leaf in table.childList)
            {
                if (leaf.IsIndex.Equals("True"))
                {
                    listIndexInfo.Add(leaf);
                    m_ModifyIndexGrade++;
                }                    
            }

            int i = 0;
			ObservableCollection<DyDataGrid_MIBModel> datalist = new ObservableCollection<DyDataGrid_MIBModel>();
			Dictionary<string, string> temdicValue = new Dictionary<string, string>();//保存当前选中行的信息，key为nameMib，value为值
			Dictionary<string, string> temdicOid = new Dictionary<string, string>();//保存当前选中行的信息，key为nameMib，value为Oid
			if (cmdMibInfo == null)
				return false;

			foreach (var iter in mibModel.Properties)
			{
				dynamic model = new DyDataGrid_MIBModel();
				if (iter.Key.Equals("indexlist"))
					continue;

				MibLeaf mibLeaf = SnmpToDatabase.GetMibNodeInfoByName(iter.Key, CSEnbHelper.GetCurEnbAddr());

				if (mibLeaf == null)
					continue;

				if (iter.Value is DataGrid_Cell_MIB)
				{
					var cellGrid = iter.Value as DataGrid_Cell_MIB;

					temdicValue.Add(mibLeaf.childNameMib, cellGrid.m_Content);
					temdicOid.Add(mibLeaf.childNameMib, cellGrid.oid);
				}
				else if (iter.Value is DataGrid_Cell_MIB_ENUM)
				{
					var cellGrid = iter.Value as DataGrid_Cell_MIB_ENUM;

					temdicValue.Add(mibLeaf.childNameMib, cellGrid.m_CurrentValue.ToString());
					temdicOid.Add(mibLeaf.childNameMib, cellGrid.oid);
				}
			}

			if (cmdMibInfo.m_cmdDesc.Equals(this.Title))
			{
				if (cmdMibInfo.m_leaflist.Count > 0)
				{
					//属性节点
					foreach (string oid in cmdMibInfo.m_leaflist)
					{
						MibLeaf mibLeaf = Database.GetInstance().GetMibDataByOid(oid, CSEnbHelper.GetCurEnbAddr());
						dynamic model = new DyDataGrid_MIBModel();
						string devalue = null;
						string stroid = null;
                        if (temdicValue.ContainsKey(mibLeaf.childNameMib))
                            devalue = temdicValue[mibLeaf.childNameMib];
                        else
                            continue;

						if (temdicOid.ContainsKey(mibLeaf.childNameMib))
							stroid = temdicOid[mibLeaf.childNameMib];
						else
                            continue; ;

						model.AddParaProperty("ParaName", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.childNameCh,
							oid = stroid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "参数名称");

						// 在这里要区分DataGrid要显示的数据类型;
						var dgm = DataGridCellFactory.CreateGridCell(mibLeaf.childNameMib, mibLeaf.childNameCh, devalue, stroid, CSEnbHelper.GetCurEnbAddr());

						model.AddParaProperty("ParaValue", dgm, "参数值");

						model.AddParaProperty("ParaValueRange", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.managerValueRange,
							oid = stroid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "取值范围");

						model.AddParaProperty("ParaUnit", new DataGrid_Cell_MIB()
						{
							m_Content = mibLeaf.unit,
							oid = stroid,
							MibName_CN = mibLeaf.childNameCh,
							MibName_EN = mibLeaf.childNameMib
						}, "单位");

						// 将这个整行数据填入List;
						if (model.Properties.Count != 0)
						{
							// 向单元格内添加内容;
							datalist.Add(model);
							i++;
						}

						// 最终全部收集完成后，为控件赋值;
						if (i == datalist.Count)
						{
							this.ParaDataModel = model;
							this.DynamicParaSetGrid.DataContext = datalist;
						}
					}
				}
			}

			return true;
		}

        public void InitQueryParaSetGrid(CmdMibInfo mibInfo, MibTable table, int operType)
        {
            cmdMibInfo = mibInfo;
            m_operType = operType;
            m_MibTable = table;

            listIndexInfo.Clear();
            foreach (MibLeaf leaf in table.childList)
            {
                if (leaf.IsIndex.Equals("True"))
                    listIndexInfo.Add(leaf);
            }

            this.Title = mibInfo.m_cmdDesc;
            int i = 0;
            ObservableCollection<DyDataGrid_MIBModel> datalist = new ObservableCollection<DyDataGrid_MIBModel>();
            if (cmdMibInfo == null)
                return;

            if (cmdMibInfo.m_cmdDesc.Equals(this.Title))
            {
                if (listIndexInfo.Count > 0)
                {
                    //索引节点
                    foreach (MibLeaf mibLeaf in listIndexInfo)
                    {
                        dynamic model = new DyDataGrid_MIBModel();
                        model.AddParaProperty("ParaName", new DataGrid_Cell_MIB()
                        {
                            m_Content = mibLeaf.childNameCh,
                            oid = mibLeaf.childOid,
                            MibName_CN = mibLeaf.childNameCh,
                            MibName_EN = mibLeaf.childNameMib
                        }, "参数名称");

                        model.AddParaProperty("ParaValue", new DataGrid_Cell_MIB()
                        {
                            m_Content = mibLeaf.defaultValue,
                            oid = mibLeaf.childOid,
                            MibName_CN = mibLeaf.childNameCh,
                            MibName_EN = mibLeaf.childNameMib
                        }, "参数值");

                        model.AddParaProperty("ParaValueRange", new DataGrid_Cell_MIB()
                        {
                            m_Content = mibLeaf.managerValueRange,
                            oid = mibLeaf.childOid,
                            MibName_CN = mibLeaf.childNameCh,
                            MibName_EN = mibLeaf.childNameMib
                        }, "取值范围");

                        model.AddParaProperty("Unit", new DataGrid_Cell_MIB()
                        {
                            m_Content = mibLeaf.unit,
                            oid = mibLeaf.childOid,
                            MibName_CN = mibLeaf.childNameCh,
                            MibName_EN = mibLeaf.childNameMib
                        }, "单位");

                        // 将这个整行数据填入List;
                        if (model.Properties.Count != 0)
                        {
                            // 向单元格内添加内容;
                            datalist.Add(model);
                            i++;
                        }
                        // 最终全部收集完成后，为控件赋值;
                        if (i == datalist.Count)
                        {
                            this.ParaDataModel = model;
                            this.DynamicParaSetGrid.DataContext = datalist;
                        }
                    }
                }
            }
        }

		/// <summary>
		/// 对无效的值"X",根据取值范围进行转换
		/// </summary>
		/// <param name="leaf"></param>
		/// <returns></returns>
		private string ConvertValidValue(MibLeaf leaf)
		{
			string value = leaf.defaultValue;
			if (leaf.defaultValue.Equals("×"))
			{
				if (leaf.OMType.Equals("u32") || leaf.OMType.Equals("s32"))
				{
					string[] str = leaf.managerValueRange.Split('-');
					value = str[0];
				}

				if (leaf.OMType.Equals("enum"))
				{
					// 1.取出该节点的取值范围
					var mvr = leaf.managerValueRange;

					// 2.分解取值范围
					var mapKv = MibStringHelper.SplitManageValue(mvr);
					if (!mapKv.ContainsValue(value))
						value = mapKv.FirstOrDefault().Key.ToString();
				}
			}
			else
			{
				string[] devalue = leaf.defaultValue.Split(':');
				value = devalue[0];
			}

			return value;
		}

		public DyDataGrid_MIBModel ParaDataModel
		{
			get
			{
				return m_ParaModel;
			}
			set
			{
				m_ParaModel = value;
				this.DynamicParaSetGrid.Columns.Clear();

				// 获取所有列信息，并将列信息填充到DataGrid当中;
				foreach (var iter in m_ParaModel.PropertyList)
				{
					if (iter.Item1.Equals("ParaValue"))
					{
						DataGridTemplateColumn column = new DataGridTemplateColumn();
						DataTemplate TextBlockTemplate = new DataTemplate();
						DataTemplate ComboBoxTemplate = new DataTemplate();

						string textblock_xaml =
						   @"<DataTemplate xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                            xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                            xmlns:model='clr-namespace:WPF.Model'>
                                <TextBlock Text='{Binding " + iter.Item1 + @".m_Content}'/>
                            </DataTemplate>";

						string combobox_xaml =
						   @"<DataTemplate xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                            xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                            xmlns:model='clr-namespace:WPF.Model'>
                                <ComboBox IsEditable='True' IsReadOnly='False' ItemsSource='{Binding " + iter.Item1 + @".m_AllContent}' SelectedIndex='0'/>
                             </DataTemplate>";

						TextBlockTemplate = XamlReader.Parse(textblock_xaml) as DataTemplate;
						ComboBoxTemplate = XamlReader.Parse(combobox_xaml) as DataTemplate;

						column.Header = iter.Item2;                                      // 填写列名称;
						column.CellTemplate = TextBlockTemplate;                         // 将单元格的显示形式赋值;
						column.CellEditingTemplate = ComboBoxTemplate;                   // 将单元格的编辑形式赋值;
						column.Width = 230;                                              // 设置显示宽度;

						this.DynamicParaSetGrid.Columns.Add(column);
					}
					/*else if(iter.Item1.Equals("ParaSelect"))
                    {
                        DataGridTemplateColumn column = new DataGridTemplateColumn();
                        DataTemplate checkBoxHeaderTemplate = new DataTemplate();
                        DataTemplate checkBoxTemplate = new DataTemplate();

                        string checkBoxHeader_xaml =
                           @"<DataTemplate xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                            xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                            xmlns:model='clr-namespace:WPF.Model'>
                                <CheckBox IsChecked='{Binding " + @"m_bAllSelect}'/>
                            </DataTemplate>";

                        string checkBox_xaml =
                           @"<DataTemplate xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                            xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                            xmlns:model='clr-namespace:WPF.Model'>
                                <CheckBox IsChecked='{Binding " + iter.Item1 + @".m_bIsSelected}'/>
                            </DataTemplate>";

                        checkBoxHeaderTemplate = XamlReader.Parse(checkBoxHeader_xaml) as DataTemplate;
                        checkBoxTemplate = XamlReader.Parse(checkBox_xaml) as DataTemplate;

                        //DataGridCheckBoxColumn column = new DataGridCheckBoxColumn();
                        //column.HeaderTemplate = CheckBoxTemplate;
                        column.HeaderTemplate = checkBoxHeaderTemplate;
                        column.CellTemplate = checkBoxTemplate;

                        this.DynamicParaSetGrid.Columns.Add(column);
                    }*/
                    else
					{
						// 当前添加的表格类型只有Text类型，应该使用工厂模式添加对应不同的数据类型;
						var column = new DataGridTextColumn
						{
							Header = iter.Item2,
							IsReadOnly = true,
							Binding = new Binding(iter.Item1 + ".m_Content")
						};

						this.DynamicParaSetGrid.Columns.Add(column);
					}
				}
			}
		}
	}
}