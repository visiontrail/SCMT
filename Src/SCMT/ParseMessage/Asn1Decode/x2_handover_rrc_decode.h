/*******************************************************************************
* COPYRIGHT DaTang Mobile Communications Equipment CO.,LTD
********************************************************************************
* 文件名称: 
* 功能描述: 
* 其他说明: 
* 编写日期: 
* 修改历史: 
* 修改日期    修改人  BugID/CRID      修改内容
* ------------------------------------------------------------------------------
* 
*******************************************************************************/

/******************************** 头文件保护开头 ******************************/
#ifndef X2AP_PER_RRC_DECODE_H
#define X2AP_PER_RRC_DECODE_H

/*******************************  头文件包含  *********************************/
#include "cdl_common.h"
/******************************** 宏和常量定义 ********************************/

/********************************  类型定义  **********************************/
/*请求消息解码结构*/
typedef struct
{
    u32             u32SourceEnbId;             /* Source Enb ID */ 
    u8              u8SourceCellId;             /* Source cell ID */
    u16             u16SourcePci;               /* Source Pci */
    u16             u16SourceDlEarfcn;          /* Source小区中心频道数 */
    u16             u16SourceCrnti;             /* Source小区用户的CRNTI */
}RRC_HO_PrepInfoDecode;

/****************************  函数原型声明  ******************************/
void RRC_HO_PrepInfoDecodeFunc(void *pMsg, RRC_HO_PrepInfoDecode *pstruDecode);
/******************************** 头文件保护结尾 ******************************/
#endif
/******************************  头文件结束  **********************************/

