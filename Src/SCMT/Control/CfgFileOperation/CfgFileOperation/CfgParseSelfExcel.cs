﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Excel = Microsoft.Office.Interop.Excel;

namespace CfgFileOperation
{
    /// <summary>
    /// 自定义_初配数据文件
    /// </summary>
    class CfgParseSelfExcel
    {

        Dictionary<string, string> SheetCellCol = null;

        public CfgParseSelfExcel()
        {
            SheetCellCol = new Dictionary<string, string>(){
                { "TableName" ,"A" },//表名
                { "Index" ,"B" },//索引
                { "NodeName" ,"C" },//字段名称
                { "NodeValue" ,"D" },//取值
            };
        }

        /// <summary>
        /// 总体处理 reclist
        /// </summary>
        /// <param name="strExcelPath"></param>
        /// <param name="strFileToDirectory"></param>
        /// <param name="strUeType"></param>
        public bool ProcessingExcel(BinaryWriter bw, string strExcelPath, string strFileToDirectory, string strCondition, CfgOp cfgOp)
        {
            if ((String.Empty == strExcelPath) || (String.Empty == strFileToDirectory) || (null == cfgOp))
                return false;

            // "init" 页
            if (0 == String.Compare("init", strCondition, true)) // 不区分大小写，相等
            {
                ExportSelfExcelForInit(strExcelPath, strCondition, cfgOp);
            }
            // "patch" 页
            else if (0 == String.Compare("patch", strCondition, true))
            {
                ExportSelfExcelForPatch(bw, strExcelPath, strCondition, cfgOp);
            }
            return true;
        }
        /// <summary>
        /// 总体处理 reclist
        /// </summary>
        /// <param name="strExcelPath"></param>
        /// <param name="strFileToDirectory"></param>
        /// <param name="strUeType"></param>
        public bool ProcessingExcel5G(BinaryWriter bw, string strExcel, string strFile, string strCon, 
            CfgOp cfgOp, CfgParseReclistExcel5G reclist, string strUeType)
        {
            if ((String.Empty == strExcel) || (String.Empty == strFile) || (null == reclist)
                || (String.Empty == strUeType) || (String.Empty == strCon) || (null == cfgOp))
                return false;
            bool re = true;
            // "patch" 页
            if (0 == String.Compare("patch", strCon, true))
            {
                if (!ExportSelfExcelForPatch5G(bw, strExcel, strCon, cfgOp, reclist, strUeType))
                {
                    re = false;
                    bw.Write(String.Format("Err CfgParseSelfExcel 5G patch, return err.\n").ToArray());
                }
            }
            else
            {
                bw.Write(String.Format("Err CfgParseSelfExcel 5G patch, Condition({0}) is not 'patch'.\n", strCon).ToArray());
                re = false;
            }
            return re;
        }

        /// <summary>
        /// init 页的处理
        /// </summary>
        /// <param name="wbook"></param>
        /// <param name="excelOp"></param>
        void ExportSelfExcelForInit(string FilePath, string sheetName, CfgOp cfgOp)
        {
            var excelOp = CfgExcelOp.GetInstance();
            int rowCount = GetEndLineNum(FilePath, sheetName);                                        // 获取行数
            if (-1 == rowCount)
            {
                //bw.Write(String.Format("Err:ProcessingExcelRru ({0}):({1}), get row count err.", strExcelPath, strSheet));
                return;// false;
            }
            Dictionary<string, object[,]> ColVals = GetSheetColInfos(FilePath, sheetName, rowCount); // 获取所有sheet 每col的数据
            string strCurTableName = "";                                              // 保存当前表名
            string strCurIndex = "";                                                  // 保存当前索引
            // 逐行分析
            for (int currentLine = 2; currentLine < rowCount + 1; currentLine++)
            {
                if (!IsExistTableName(cfgOp, ColVals, currentLine))                 //判断 : 查看是否有这个表
                    break;//

                string TableName = GetCellValue(ColVals, currentLine, "TableName"); //表名
                string Index = GetCellValue(ColVals, currentLine, "Index");         //索引
                string NodeName = GetCellValue(ColVals, currentLine, "NodeName");   //叶子名
                string NodeValue = GetCellValue(ColVals, currentLine, "NodeValue"); //叶子值
                strCurTableName = SetCurTableName(TableName, strCurTableName);      //更新表名
                //if (strCurTableName == "drxQciCfgEntry")
                //{
                //    Console.WriteLine("...");
                //}
                strCurIndex = SetCurIndex(Index, strCurIndex);                      //更新索引
                
                if (!WriteValueToBuffer(cfgOp.m_mapTableInfo[strCurTableName],      //更新节点值
                    strCurIndex, NodeName, NodeValue))
                    continue;
            }
        }

        /// <summary>
        /// patch页的处理，用于制作nb_patch.cfg
        /// </summary>
        /// <param name="wks"></param>
        /// <param name="cfgOp"></param>
        bool ExportSelfExcelForPatch(BinaryWriter bw, string FilePath, string sheetName, CfgOp cfgOp)
        {
            var excelOp = CfgExcelOp.GetInstance();
            int rowCount = GetEndLineNum(FilePath, sheetName);                     // 获取行数
            if (-1 == rowCount)
            {
                bw.Write(String.Format("Err:ExportSelfExcelForPatch ({0}):({1}), get row count err.", FilePath, sheetName).ToArray());
                return false;
            }
            Dictionary<string, object[,]> ColVals = GetSheetColInfos(FilePath, sheetName, rowCount);  // 获取所有sheet 每col的数据
            string strCurTableName = "";                                              // 保存当前表名
            string strCurIndex = "";                                                  // 保存当前索引
            for (int currentLine = 2; currentLine < rowCount + 1; currentLine++)      // 逐行分析
            {
                if (!IsExistTableName(cfgOp, ColVals, currentLine))                   // 判断 : 查看是否有这个表
                    break;//

                string TableName = GetCellValue(ColVals, currentLine, "TableName");   // 表名
                string Index = GetCellValue(ColVals, currentLine, "Index");           // 索引
                string NodeName = GetCellValue(ColVals, currentLine, "NodeName");     // 叶子名
                string NodeValue = GetCellValue(ColVals, currentLine, "NodeValue");   // 叶子值
                strCurTableName = SetCurTableName(TableName, strCurTableName);        // 更新表名
                
                // 标记 表名和实例信息
                if (0 != String.Compare("", Index, true))
                {
                    cfgOp.m_reclistExcel.InsertPdgTab(strCurTableName);               // 增加要写Patch的表名
                    if (0 == String.Compare("-1", Index, true))                       // 索引 == -1 相关的处理
                    {
                        PatchNeedWritePatchTable(cfgOp, strCurTableName);             // 需要做补丁文件的表名和表下面的节点     
                        continue;
                    }
                    else                                                              // 索引 剩下的不为空 相关处理
                        strCurIndex = SetCurIndex(Index, strCurIndex);                // 更新索引
                }
                
                if (!WriteValueToBuffer(cfgOp.m_mapTableInfo[strCurTableName],        // 更新节点值
                    strCurIndex, NodeName, NodeValue))
                    continue;
            }
            return true;
        }

        /// <summary>
        /// patch页的处理，用于制作nb_patch.cfg
        /// </summary>
        /// <param name="wks"></param>
        /// <param name="cfgOp"></param>
        bool ExportSelfExcelForPatch5G(BinaryWriter bw, string FilePath, string sheetName, 
            CfgOp cfgOp, CfgParseReclistExcel5G reclist, string strUeType)
        {
            var excelOp = CfgExcelOp.GetInstance();
            int rowCount = GetEndLineNum(FilePath, sheetName);                     // 获取行数
            if (-1 == rowCount)
            {
                bw.Write(String.Format("Err:ExportSelfExcelForPatch ({0}):({1}), get row count err.", FilePath, sheetName).ToArray());
                return false;
            }
            Dictionary<string, object[,]> ColVals = GetSheetColInfos(FilePath, sheetName, rowCount);  // 获取所有sheet 每col的数据
            string strCurTableName = "";                                              // 保存当前表名
            string strCurIndex = "";                                                  // 保存当前索引
            for (int currentLine = 2; currentLine < rowCount + 1; currentLine++)      // 逐行分析
            {
                if (!IsExistTableName(cfgOp, ColVals, currentLine))                 // 判断 : 查看是否有这个表
                    break;//

                string TableName = GetCellValue(ColVals, currentLine, "TableName");   // 表名
                string Index = GetCellValue(ColVals, currentLine, "Index");           // 索引
                string NodeName = GetCellValue(ColVals, currentLine, "NodeName");     // 叶子名
                string NodeValue = GetCellValue(ColVals, currentLine, "NodeValue");   // 叶子值
                strCurTableName = SetCurTableName(TableName, strCurTableName);        // 更新表名
                if (String.Equals("logFileConfigurationEntry", strCurTableName))
                {
                    Console.WriteLine("==");
                }
                if (!bIsSpecialScenc(strCurTableName,  strUeType)) { continue; }      // 特殊处理

                // 标记 表名和实例信息
                if (0 != String.Compare("", Index, true))
                {
                    //cfgOp.m_reclistExcel.InsertPdgTab(strCurTableName);             // 增加要写Patch的表名
                    reclist.InsertPdgTab(strCurTableName, "1", cfgOp);                // 增加要写Patch的表名
                    if (0 == String.Compare("-1", Index, true))                       // 索引 == -1 相关的处理
                    {
                        PatchNeedWritePatchTable(cfgOp, strCurTableName, reclist);    // 需要做补丁文件的表名和表下面的节点     
                        continue;
                    }
                    else                                                              // 索引 剩下的不为空 相关处理
                        strCurIndex = SetCurIndex(Index, strCurIndex);                // 更新索引
                }
                // 5G 索引和节点键值对，需要保存新的位置
                var recTable = reclist.GetReclistTableByName(strCurTableName);
                if (!recTable.tableInfo.IsExistInstsByIndex(strCurIndex))//是否存在这个索引值的实例
                    continue;
                recTable.AddLeafNameByIndex(strCurIndex, NodeName);
                // 更新节点值
                if (!WriteValueToBuffer(recTable.tableInfo, strCurIndex, NodeName, NodeValue))
                    continue;
            }
            return true;
        }
        /// <summary>
        ///  "1:展讯" , "2:e500" , "3:华为" 模式下的 5个表不处理
        /// </summary>
        /// <param name="TableName"></param>
        /// <param name="strUeType"></param>
        /// <returns></returns>
        bool bIsSpecialScenc(string TableName, string strUeType)
        {
            //2018-11-16 quyaxin 根据场景支持个性化配置是否写入告警\RRU相关信息
            List<string> specicalTable = new List<string>() { "alarmCauseEntry", "rruTypeEntry" ,"rruTypePortEntry",
                "packStoreOptimizeControl" , "antennaBfScanWeightEntry" };
            List<string> specicalType = new List<string>() { "1:展讯" , "2:e500" , "3:华为" };

            if (-1 != specicalType.IndexOf(strUeType))// 先筛选 uetype, 在选表
            {
                if (-1 != specicalTable.IndexOf(TableName))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// //需要做补丁文件的表和它的实例节点
        /// </summary>
        /// <param name="cfgOp"></param>
        /// <param name="strCurTableName"></param>
        void PatchNeedWritePatchTable(CfgOp cfgOp, string strCurTableName)
        {
            //需要做补丁文件的表名
            cfgOp.m_reclistExcel.InsertPdgTab(strCurTableName);               //增加要写Patch的表名
            //需要做补丁文件的表下面的节点
            List<CfgTableInstanceInfos> m_cfgInsts = cfgOp.m_mapTableInfo[strCurTableName].m_cfgInsts;//实例
            List<CfgFileLeafNodeOp> m_LeafNodes = cfgOp.m_mapTableInfo[strCurTableName].m_LeafNodes;  //节点;
            foreach (var cfgInst in m_cfgInsts)
            {
                string strInstIndex = cfgInst.GetInstantNum();//==string.Empty ? "" : cfgInst.GetInstantNum();
                foreach (var leafNode in m_LeafNodes)
                {
                    string strMibName = leafNode.m_struMibNode.strMibName;
                    cfgOp.m_mapTableInfo[strCurTableName].InsertInstOrLeaf(strInstIndex, strMibName);
                }
            }
        }
        /// <summary>
        /// 5G 
        /// </summary>
        /// <param name="cfgOp"></param>
        /// <param name="strCurTableName"></param>
        /// <param name="reclist"></param>
        void PatchNeedWritePatchTable(CfgOp cfgOp, string strCurTableName, CfgParseReclistExcel5G reclist)
        {
            //需要做补丁文件的表名
            reclist.InsertPdgTab(strCurTableName, "1", cfgOp);//增加要写Patch的表名
            //需要做补丁文件的表下面的节点
            ReclistTable recTable = reclist.GetReclistTableByName(strCurTableName);
            if (null == recTable)
                return;
            foreach (var cfgInst in recTable.tableInfo.m_cfgInsts)//实例
            {
                foreach (var leafNode in recTable.tableInfo.m_LeafNodes)//节点;
                {
                    recTable.AddLeafNameByIndex(cfgInst.GetInstantNum(), leafNode.m_struMibNode.strMibName);
                }
            }
        }

        /// <summary>
        /// 与 reclist中的WriteValueToBuffer功能相同
        /// </summary>
        /// <param name="curtable"></param>
        /// <param name="strCurIndex"></param>
        /// <param name="strNodeName"></param>
        /// <param name="strNodeValue"></param>
        /// <returns></returns>
        bool WriteValueToBuffer(CfgTableOp curtable, string strCurIndex, string strNodeName, string strNodeValue)
        {
            CfgTableInstanceInfos pInstInfo = null;  //实例信息
            CfgFileLeafNodeOp leafNodeOp = null;     //节点属性
            int InstsPos = 0;                        //实例位置

            // 获得表的某个(strIndex)实例信息
            if (!curtable.GetCfgInstsByIndex(strCurIndex, out pInstInfo, out InstsPos))//是否存在这个索引值的实例
                return false;

            // 获得表的某个节点(strNodeName)信息
            if (!curtable.GetLeafNodesByNodeName(strNodeName, out leafNodeOp))
                return false;

            // 组合节点的信息
            byte[] InstMem = pInstInfo.GetInstMem();                           //1.这个表实例的内容的内存;实例化后表中节点依次排列的内容
            ushort u16FieldLen = leafNodeOp.m_struFieldInfo.u16FieldLen;       //2.这个节点字段的长度
            ushort u16FieldOffset = leafNodeOp.m_struFieldInfo.u16FieldOffset; //3.这个节点在实例内存中相对的位置 ;字段相对记录头偏移量
            string strOMType = leafNodeOp.m_struMibNode.strOMType;
            string asnType = leafNodeOp.m_struMibNode.strMibSyntax;            //asnType
            string strDefaultValue = strNodeValue;                             // 修改点, 在InstMem中把strNodeName的值修改为strNodeValue

            // 修改内存数据
            List<byte[]> byteArray = new List<byte[]>() { InstMem };
            List<int> bytePosL = new List<int>() { u16FieldOffset };
            new CfgOp().WriteToBuffer(byteArray, strDefaultValue, bytePosL, strOMType, u16FieldLen, "", asnType);

            // 重新写回去,实验证明数据已经修改，而且不会修改其他字段
            // 在patch写的时候，需要再次写回去。。。
            curtable.SetInstsInfoValueByIndex(InstsPos, byteArray[0]);
            return true;
        }

        /// <summary>
        /// 是否是存在的表
        /// </summary>
        /// <param name="strTableName"></param>
        /// <param name="cfgOp"></param>
        /// <returns></returns>
        bool IsExistTableName(CfgOp cfgOp, Dictionary<string, object[,]> ColVals, int currentLine)
        {
            string strTableName = GetCellValue(ColVals, currentLine, "TableName"); //表名
            if (0 != String.Compare(strTableName, "", true))
            {
                strTableName = strTableName.Replace("Table", "Entry");
                if (!cfgOp.m_mapTableInfo.ContainsKey(strTableName))//再次查看是否有这个表
                    return false;//pCurTabOp->InitialMibTableInfo(pAdoconnection, strCurTableName);
            }
            return true;
        }
        
        /// <summary>
        /// 判断是否结束行
        /// </summary>
        /// <param name="wks"></param>
        /// <param name="ColName"></param>
        /// <returns></returns>
        int GetEndLineNum(string FilePath, string sheetName)
        {
            var excelOp = CfgExcelOp.GetInstance();
            int rowCount = excelOp.GetRowCount(FilePath, sheetName);
            if (-1 == rowCount)
            {
                //bw.Write(String.Format("Err:ProcessingExcelRru ({0}):({1}), get row count err.", strExcelPath, strSheet));
                return -1;// false;
            }
            object[,] arry = excelOp.GetRangeVal(FilePath, sheetName, "A" + "1", "A" + rowCount);
            for (int row = 1; row < rowCount + 1; row++)
            {
                var cellVar = arry[row, 1];
                if (cellVar == null)
                    continue;
                if (0 == String.Compare("end", cellVar.ToString(), true))
                    return row;
            }
            return rowCount;
        }

        /// <summary>
        /// 更新新的表名(条件是非"")
        /// </summary>
        /// <param name="strTableName"></param>
        /// <param name="strCurTableName"></param>
        /// <returns></returns>
        string SetCurTableName(string strTableName, string strCurTableName)
        {
            return String.Compare(strTableName, "", true) == 0 ? strCurTableName : strTableName.Replace("Table", "Entry");
        }
        /// <summary>
        /// 更新索引值
        /// </summary>
        /// <param name="strIndex"></param>
        /// <param name="strCurIndex"></param>
        /// <returns></returns>
        string SetCurIndex(string strIndex, string strCurIndex)
        {
            if (0 != String.Compare(strIndex, "", true))
            {
                strCurIndex = "." + (0 < strIndex.IndexOf('_') ? strIndex.Replace("_", ".") : strIndex);
            }
            return strCurIndex;
        }
        /// <summary>
        /// 获取单元格值
        /// </summary>
        /// <param name="ColVals"></param>
        /// <param name="currentLine"></param>
        /// <param name="strKey"></param>
        /// <returns></returns>
        string GetCellValue(Dictionary<string, object[,]> ColVals, int currentLine, string strKey)
        {
            var cellVal = ColVals[strKey][currentLine, 1];
            return cellVal == null ? "" : cellVal.ToString();
        }

        /// <summary>
        /// 组合信息
        /// </summary>
        /// <param name="wks"></param>
        /// <param name="rowCount"></param>
        /// <returns></returns>
        Dictionary<string, object[,]> GetSheetColInfos(string FilePath, string sheetName, int rowCount)
        {
            var excelOp = CfgExcelOp.GetInstance();
            // 获取所有sheet 每col的数据
            Dictionary<string, object[,]> ColVals = new Dictionary<string, object[,]>();
            foreach (var colName in SheetCellCol.Keys)//
            {
                object[,] arry = excelOp.GetRangeVal(FilePath, sheetName, SheetCellCol[colName] + "1", SheetCellCol[colName] + rowCount);
                ColVals.Add(colName, arry);
            }
            return ColVals;
        }

    }//class end

}
