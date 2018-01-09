﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using EAM.Data.Domain;
using EAM.Data.ImportAndExport.AssetsDiff;
using EAM.Data.ImportAndExport.Export;
using EAM.Data.ImportAndExport.Import;
using EAM.Data.Services;
using EAM.Data.Services.Query;
using Eam.Web.Portal.Areas.SycData.Models;
using Eam.Web.Portal._Comm;
using Eam.Core.Zip;


namespace Eam.Web.Portal.Areas.SycData.Controllers
{
    public class SycController : EamAdminController
    {
        private IAssetsService _assetsService;
        private IUnImportAssetsService _unImportAssetsService;
        private IImportHistoryService _importHistoryService;
        private IUserService _userService;
        private IRoleService _roleService;//2017-05-31 wnn
        private readonly ISystemService _sysService;//2017-06-05 wnn

        public static IDictionary<Guid, ProgressInfo> ImportTasks = new Dictionary<Guid, ProgressInfo>();
        // private static Guid crntTaskGuid;
        public static double ImportProgress; //进度
        public SycController(IAssetsService assetsService,
            IImportHistoryService importHistoryService,
            IUnImportAssetsService unImportAssetsService,
            IUserService userService,//添加一个定义必须添加 2017-05-26 wnn
            IRoleService roleService,//2017-05-31 wnn
            ISystemService sysService//2017-06-05 wnn
          )
        {
            _assetsService = assetsService;
            _importHistoryService = importHistoryService;
            _unImportAssetsService = unImportAssetsService;
            _userService = userService;//添加一个定义必须添加 2017-05-26 wnn
            _roleService = roleService;//2017-05-31 wnn
            _sysService = sysService;//2017-06-05 wnn
            /*_unImportHistoryService = unImportHistoryService;*/
        }

        #region 办学导入

        [Permission(EnumBusinessPermission.SycData_Syc_BanXue)]
        public ActionResult Index()
        {
            return View(new ImportResultModel { });
        }


        /// <summary>
        /// Creates the folder if needed.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        private bool CreateFolderIfNeeded(string path)
        {
            bool result = true;
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception)
                {
                    /*TODO: You must process this exception.*/
                    result = false;
                }
            }
            return result;
        }



        [HttpPost]
        [Permission(EnumBusinessPermission.SycData_Syc_BanXue)]
        // public virtual ActionResult ImportBxAssets(HttpPostedFileBase file)
        public virtual JsonResult ImportBxAssets(HttpPostedFileBase file/*,bool isRequestGuid*/)
        {
            ProgressInfo progressInfo = new ProgressInfo();

            bool isUploaded = false;
            string message = "File upload failed";
            //progressInfo.ImportedPercentVal = 1;
            if (file != null && (0 != file.ContentLength)) //判断文件非空
            {
                var fileName = string.Format("办学_{0}{1}", DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Path.GetExtension(file.FileName));
                var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);

                try
                {
                    //progressInfo.ImportedPercentVal = 2;
                    file.SaveAs(saveFileName); //保存文件
                    //progressInfo.ImportedPercentVal = 3;
                }
                catch (Exception ex)
                {
                    message = "保存文件失败！" + ex;
                    isUploaded = true;
                    //progressInfo.ImportedPercentVal = 100;
                    //return Json(new { isUploaded = true, message });
                }

                var taskId = Guid.NewGuid();
                ImportTasks.Add(taskId, progressInfo);

                var task = Task.Factory.StartNew(() =>
                {



                    //    var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);
                    //
                    //导入历史
                    ImportHistory importHistory = new ImportHistory
                    {
                        ImportFile = fileName,
                        ImportTime = DateTime.Now,
                        ImportType = 1, //导入类型
                        UserId = base.UserId
                    };
                    importHistory = _importHistoryService.Add(importHistory);

                    try
                    {
                        var bxImport = new BxImport(_assetsService, _importHistoryService, _unImportAssetsService);
                        var result = bxImport.DoImport(saveFileName, ref progressInfo);

                        importHistory.TotalRows = result.Imported.Count + result.UnImported.Count;
                        importHistory.ImportRows = result.Imported.Count;
                        _importHistoryService.Update(importHistory);
                        message = "导入资产成功！";



                    }
                    catch (Exception ex)
                    {
                        importHistory.Exception = ex.Message;
                        _importHistoryService.Update(importHistory);
                        message = "导入资产异常！" + ex;

                    }


                    isUploaded = true;
                    ImportTasks.Remove(taskId);

                });



            }
            else
            {
                message = "上传文件失败！";
                isUploaded = true;

            }

            return Json(new { isUploaded = true, message });
        }
        /// <summary>
        /// 为了避免在原始方法上修改 新建的方法 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public JsonResult NewBXDataInput(HttpPostedFileBase file)
        {

            //
            // 导入结果定义
            //
            JsonResult result = new JsonResult();

            //
            // 导入过程信息
            //
            ProgressInfo progressInfo = new ProgressInfo();
            //progressInfo.ImportedPercentVal = 1;                        // 导入进度百分比


            //
            // 导入历史
            //
            ImportHistory importHistory;


            //
            // 导入文件判空
            //
            if (file == null)
            {
                result.Data = new { type = 1, msg = "请选择导入文件！" };
                return result;
            }

            //
            // 将导入文件存储到临时文件夹
            //
            var fileName = string.Format("办学_{0}{1}", DateTime.Now.ToString("yyyyMMddHHmmss"),
                Path.GetExtension(file.FileName));
            var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);

            //
            // 填写导入历史信息
            //
            importHistory = new ImportHistory
            {
                ImportFile = fileName,
                ImportTime = DateTime.Now,
                ImportType = 1,  //导入类型
                UserId = base.UserId
            };

            //
            // 将导入历史写到数据库
            //
            try
            {
                progressInfo.ImportedPercentVal = 2;
                file.SaveAs(saveFileName);                      // 导入文件上传到服务器
                progressInfo.ImportedPercentVal = 3;

            }
            catch (Exception ex)
            {
                result.Data = new { type = 1, msg = "上传文件异常！" + ex };
                return result;

            }
            var taskId = Guid.NewGuid();
            ImportTasks.Add(taskId, progressInfo);
            importHistory = _importHistoryService.Add(importHistory);
            //
            // 读取导入文件并写入数据库
            //
            try
            {
                //
                // 提取文件数据
                //
                var bxImport = new BxImport(_assetsService, _importHistoryService, _unImportAssetsService);

                //
                // 将提取的数据写入数据库
                //
                var result1 = bxImport.DoImport(saveFileName, ref progressInfo);


                //
                // 更新导入历史数据

                importHistory.UnImportRows = BxReader.GetRows() - progressInfo.ImportedAssetsNum+1;
                importHistory.ImportRows = progressInfo.ImportedAssetsNum;
                importHistory.TotalRows = progressInfo.TotalAssetsNum;
                _importHistoryService.Update(importHistory);

                //
                // 导入结果填写并返回
                //
                result.Data = new
                {
                    type = 0,
                    //ImportedItems = result1.Imported,
                    //  UnImportedItems = result1.UnImported,  //！！！不能用
                    ImportedNum = importHistory.ImportRows,
                    UnImportedNum = importHistory.UnImportRows,
                    LastImportedId = _importHistoryService.LastHistory().EntityId,
                };
                return result;

            }
            catch (Exception ex)
            {




                //
                // 错误信息
                //
                importHistory.Exception = ex.Message;
                _importHistoryService.Update(importHistory);
                result.Data = new
                {
                    type = 1,
                    msg = "导入数据异常！" + ex,
                    LastImportedId = _importHistoryService.LastHistory().EntityId,
                };
                return result;
            }




        }



        // public ActionResult QueryProgress(Guid taskId)
        public JsonResult QueryProgress(/*Guid taskId*/)
        {
            //return Json(ImportTasks.Keys.Contains(taskId) ? ImportTasks[taskId] : new ProgressInfo());
            //ProgressInfo aa = ImportTasks[ImportTasks.Keys.Last()];

            return Json(ImportTasks.Keys.Count > 0 ? ImportTasks[ImportTasks.Keys.Last()] : new ProgressInfo());

        }

        //查询最新办学或动态记录ID
        [HttpPost]
        public ActionResult QueryLastImportedId(int type)
        {
            //var result = _importHistoryService.LastHistory(type).EntityId;
            var result = _importHistoryService.LastHistory(type);

            if (result != null)
            {
                return Json(result.EntityId);
            }

            return Back("无上传记录！");
        }
        [HttpPost]
        //已导入资产查询
        public ActionResult ImportedAssetsQuery(AssetsQuery model)
        {
            var result = _assetsService.QueryPage(model);
            return Json(result);
        }
        [HttpPost]
        //已导入资产查询
        public ActionResult ImportedAssetsQueryAll(AssetsQuery model)
        {
            model.PageSize = int.MaxValue;
            var result = _assetsService.QueryPage(model);
            return Json(result);
        }
        [HttpPost]
        public ActionResult DeleteAssetsQuery(int assetsid)
        {
            if (assetsid > 0)
            {

                _importHistoryService.Remove(assetsid);
                _assetsService.DeleteByimportId(assetsid);

            }
            return Back("删除成功!");

        }
        [HttpPost]
        public ActionResult DeleteAllAssetsQuery()
        {

            _importHistoryService.RemoveAll();


            return Back("删除成功!");

        }

        [HttpPost]
        //未导入资产查询
        //导入id，导入类型
        public ActionResult UnImportedAssetsQuery(int id, int type, int PageIndex = 1)
        {

            UnImportAssetsQuery model = new UnImportAssetsQuery();
            model.ImportId = id;
            model.ImportType = type;
            model.PageIndex = PageIndex;
            var result = _unImportAssetsService.QueryPage(model);

            // var result = _unImportAssetsService.QueryAllUnImport();
            return Json(result);
        }



        //导入历史信息查询

        public ActionResult ImportHistoryQuery(ImportHistoryQuery model)
        {

            var result = _importHistoryService.QueryPage(model);
            return Json(result);
        }

        #endregion

        #region 动态导入
        [HttpPost]
        [Permission(EnumBusinessPermission.SycData_Syc_Dynamic)]
        public virtual JsonResult ImportDtAssets(HttpPostedFileBase file/*,bool isRequestGuid*/)
        {



            ProgressInfo progressInfo = new ProgressInfo();

            bool isUploaded = false;
            string message = "File upload failed";
            //progressInfo.ImportedPercentVal = 1;
            if (file != null && (0 != file.ContentLength)) //判断文件非空
            {
                var fileName = string.Format("动态_{0}{1}", DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Path.GetExtension(file.FileName));
                var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);

                try
                {
                    //progressInfo.ImportedPercentVal = 2;
                    file.SaveAs(saveFileName); //保存文件
                    //progressInfo.ImportedPercentVal = 3;
                }
                catch (Exception ex)
                {
                    message = "保存文件失败！" + ex;
                    isUploaded = true;
                    //progressInfo.ImportedPercentVal = 100;
                    //return Json(new { isUploaded = true, message });
                }

                var taskId = Guid.NewGuid();
                ImportTasks.Add(taskId, progressInfo);

                //var task = Task.Factory.StartNew(() =>
                //{



                //    var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);
                //
                //导入历史
                ImportHistory importHistory = new ImportHistory
                {
                    ImportFile = fileName,
                    ImportTime = DateTime.Now,
                    ImportType = 2, //导入类型
                    UserId = base.UserId
                };
                importHistory = _importHistoryService.Add(importHistory);

                try
                {
                    //  var bxImport = new BxImport(_assetsService, _importHistoryService, _unImportAssetsService);
                    var dtImport = new DtImport(_assetsService, _importHistoryService, _unImportAssetsService);
                    var result = dtImport.DoImport(saveFileName, ref progressInfo, _importHistoryService, _unImportAssetsService);

                    importHistory.TotalRows = result.Imported.Count + result.UnImported.Count;
                    importHistory.ImportRows = result.Imported.Count;
                    _importHistoryService.Update(importHistory);
                    message = "导入资产成功！";


                }
                catch (Exception ex)
                {
                    importHistory.Exception = ex.Message;
                    _importHistoryService.Update(importHistory);
                    message = "导入资产异常！" + ex;

                }


                isUploaded = true;
                ImportTasks.Remove(taskId);

                //});



            }
            else
            {
                message = "上传文件失败！";
                isUploaded = true;

            }

            return Json(new { isUploaded = true, message });
        }





        [Permission(EnumBusinessPermission.SycData_Syc_Dynamic)]
        public ActionResult DynamicDataInput()
        {
            return View(new ImportResultModel { });
        }
        /// <summary>
        /// 盘盈导入 2017-05-25 wnn
        /// </summary>
        /// <returns></returns>
        [Permission(EnumBusinessPermission.SycData_Syc_Dynamic)]
        public ActionResult DynamicPDataInput()
        {
            return View(new ImportResultModel { });
        }

        /// <summary>
        /// 盘盈导入（数据操作）  2017-05-27 wnn
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public JsonResult NewDynamicPDataInput(HttpPostedFileBase file)
        {
            {
                //
                // 导入过程信息定义
                //
                ProgressInfo progressInfo = new ProgressInfo();
                //progressInfo.ImportedPercentVal = 1;
                JsonResult result = new JsonResult();

                //
                // 导入文件判空
                //
                if (file == null)
                {
                    result.Data = new { type = 1, msg = "请选择导入文件！" };
                    return result;
                }

                //
                // 将导入文件上传到服务器
                //
                var fileName = string.Format("盘盈_{0}{1}", DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Path.GetExtension(file.FileName));
                var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);

                //
                // 导入历史
                //
                ImportHistory importHistory;
                try
                {
                    progressInfo.ImportedPercentVal = 2;
                    file.SaveAs(saveFileName);
                    progressInfo.ImportedPercentVal = 3;
                    importHistory = new ImportHistory
                    {
                        ImportFile = fileName,
                        ImportTime = DateTime.Now,
                        ImportType = 2,
                        UserId = base.UserId
                    };
                    //importHistory = _importHistoryService.Add(importHistory);
                }
                catch (Exception ex)
                {

                    result.Data = new { type = 1, msg = "上传文件异常！" + ex };
                    return result;
                }
                var taskId = Guid.NewGuid();
                ImportTasks.Add(taskId, progressInfo);
                importHistory = _importHistoryService.Add(importHistory);

                //
                // 将导入文件写入到数据库
                //
                try
                {
                    //
                    // 将导入文件数据读取出来
                    //
                    var dtImport = new PyImport(_assetsService, _importHistoryService, _unImportAssetsService);

                    //
                    // 将提取的数据条目写入到数据库
                    //
                    var result1 = dtImport.DoImport(saveFileName, ref progressInfo, _importHistoryService, _unImportAssetsService);

                    //
                    // 更新数据导入历史

                    importHistory.UnImportRows = progressInfo.TotalAssetsNum - progressInfo.ImportedAssetsNum;
                    importHistory.ImportRows = progressInfo.ImportedAssetsNum;
                    importHistory.TotalRows = progressInfo.TotalAssetsNum;
                    _importHistoryService.Update(importHistory);

                    //
                    // 导入结果填写并返回
                    //
                    result.Data = new
                    {
                        type = 0,
                        //ImportedItems = result1.Imported,
                        //  UnImportedItems = result1.UnImported,  //！！！不能用
                        ImportedNum = importHistory.ImportRows,
                        UnImportedNum = importHistory.UnImportRows,
                        LastImportedId = _importHistoryService.LastHistory().EntityId,
                    };

                    return result;
                }
                catch (Exception ex)
                {
                    //
                    // 数据导入异常信息
                    //
                    importHistory.Exception = ex.Message;
                    _importHistoryService.Update(importHistory);
                    result.Data = new
                    {
                        type = 1,
                        msg = "导入数据异常！" + ex.Message,
                        LastImportedId = _importHistoryService.LastHistory().EntityId,
                    };
                    return result;
                }
            }
        }


        /// <summary>
        /// 盘盈导入(保存文件) 2017-05-26 wnn
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>

        [HttpPost]
        [Permission(EnumBusinessPermission.SycData_Syc_Dynamic)]
        public virtual JsonResult ImportPAssets(HttpPostedFileBase file/*,bool isRequestGuid*/)
        {



            ProgressInfo progressInfo = new ProgressInfo();

            bool isUploaded = false;
            string message = "File upload failed";
            //progressInfo.ImportedPercentVal = 1;
            if (file != null && (0 != file.ContentLength)) //判断文件非空
            {
                var fileName = string.Format("盘盈_{0}{1}", DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Path.GetExtension(file.FileName));
                var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);

                try
                {
                    //progressInfo.ImportedPercentVal = 2;
                    file.SaveAs(saveFileName); //保存文件
                    //progressInfo.ImportedPercentVal = 3;
                }
                catch (Exception ex)
                {
                    message = "保存文件失败！" + ex;
                    isUploaded = true;
                    //progressInfo.ImportedPercentVal = 100;
                    //return Json(new { isUploaded = true, message });
                }

                var taskId = Guid.NewGuid();
                ImportTasks.Add(taskId, progressInfo);

                //var task = Task.Factory.StartNew(() =>
                //{



                //    var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);
                //
                //导入历史
                ImportHistory importHistory = new ImportHistory
                {
                    ImportFile = fileName,
                    ImportTime = DateTime.Now,
                    ImportType = 2, //导入类型
                    UserId = base.UserId
                };
                importHistory = _importHistoryService.Add(importHistory);

                try
                {
                    //  var bxImport = new BxImport(_assetsService, _importHistoryService, _unImportAssetsService);
                    var dtImport = new DtImport(_assetsService, _importHistoryService, _unImportAssetsService);
                    var result = dtImport.DoImport(saveFileName, ref progressInfo, _importHistoryService, _unImportAssetsService);

                    importHistory.TotalRows = result.Imported.Count + result.UnImported.Count;
                    importHistory.ImportRows = result.Imported.Count;
                    _importHistoryService.Update(importHistory);
                    message = "导入资产成功！";


                }
                catch (Exception ex)
                {
                    importHistory.Exception = ex.Message;
                    _importHistoryService.Update(importHistory);
                    message = "导入资产异常！" + ex;

                }


                isUploaded = true;
                ImportTasks.Remove(taskId);

                //});



            }
            else
            {
                message = "上传文件失败！";
                isUploaded = true;

            }

            return Json(new { isUploaded = true, message });
        }


        //[HttpPost]
        //[Permission(EnumBusinessPermission.SycData_Syc_Dynamic)]
        //public ActionResult DynamicDataInput(HttpPostedFileBase file)
        //{
        //    if (file == null)
        //    {
        //        return View(new ImportResultModel {ErrorMessage = "请选择导入文件！"});
        //    }
        //    var fileName = string.Format("动态_{0}{1}", DateTime.Now.ToString("yyyyMMddHHmmss"),
        //        Path.GetExtension(file.FileName));
        //    var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);
        //    ImportHistory importHistory;
        //    try
        //    {
        //        file.SaveAs(saveFileName);
        //        importHistory = new ImportHistory
        //        {
        //            ImportFile = fileName,
        //            ImportTime = DateTime.Now,
        //            ImportType = 2,
        //            UserId = base.UserId
        //        };
        //        importHistory = _importHistoryService.Add(importHistory);
        //    }
        //    catch (Exception ex)
        //    {
        //        return View(new ImportResultModel {ErrorMessage = "上传文件异常！" + ex});
        //    }
        //    try
        //    {
        //        var bxImport = new DtImport(_assetsService);
        //        var result = bxImport.DoImport(saveFileName);
        //        importHistory.TotalRows = result.Imported.Count + result.UnImported.Count;
        //        importHistory.ImportRows = result.Imported.Count;
        //        _importHistoryService.Update(importHistory);
        //        return View(new ImportResultModel
        //        {
        //            ImportedItems = result.Imported,
        //            UnImportedItems = result.UnImported,
        //            ErrorMessage = "导入数据完成！"
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        importHistory.Exception = ex.Message;
        //        _importHistoryService.Update(importHistory);
        //        return View(new ImportResultModel {ErrorMessage = "导入数据异常！<br/>" + ex.Message});
        //    }
        //}
        /// <summary>
        /// 为了避免在原始方法上修改 新建的方法 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public JsonResult NewDynamicDataInput(HttpPostedFileBase file)
        {
            {
                //
                // 导入过程信息定义
                //
                ProgressInfo progressInfo = new ProgressInfo();
                //progressInfo.ImportedPercentVal = 1;
                JsonResult result = new JsonResult();

                //
                // 导入文件判空
                //
                if (file == null)
                {
                    result.Data = new { type = 1, msg = "请选择导入文件！" };
                    return result;
                }

                //
                // 将导入文件上传到服务器
                //
                var fileName = string.Format("动态_{0}{1}", DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Path.GetExtension(file.FileName));
                var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);

                //
                // 导入历史
                //
                ImportHistory importHistory;
                try
                {
                    progressInfo.ImportedPercentVal = 2;
                    file.SaveAs(saveFileName);
                    progressInfo.ImportedPercentVal = 3;
                    importHistory = new ImportHistory
                    {
                        ImportFile = fileName,
                        ImportTime = DateTime.Now,
                        ImportType = 2,
                        UserId = base.UserId
                    };
                    //importHistory = _importHistoryService.Add(importHistory);
                }
                catch (Exception ex)
                {

                    result.Data = new { type = 1, msg = "上传文件异常！" + ex };
                    return result;
                }
                var taskId = Guid.NewGuid();
                ImportTasks.Add(taskId, progressInfo);
                importHistory = _importHistoryService.Add(importHistory);

                //
                // 将导入文件写入到数据库
                //
                try
                {
                    //
                    // 将导入文件数据读取出来
                    //
                    var dtImport = new DtImport(_assetsService, _importHistoryService, _unImportAssetsService);

                    //
                    // 将提取的数据条目写入到数据库
                    //
                    var result1 = dtImport.DoImport(saveFileName, ref progressInfo, _importHistoryService, _unImportAssetsService);

                    //
                    // 更新数据导入历史

                    importHistory.UnImportRows = progressInfo.TotalAssetsNum - progressInfo.ImportedAssetsNum;
                    importHistory.ImportRows = progressInfo.ImportedAssetsNum;
                    importHistory.TotalRows = progressInfo.TotalAssetsNum;
                    _importHistoryService.Update(importHistory);

                    //
                    // 导入结果填写并返回
                    //
                    result.Data = new
                    {
                        type = 0,
                        //ImportedItems = result1.Imported,
                        //  UnImportedItems = result1.UnImported,  //！！！不能用
                        ImportedNum = importHistory.ImportRows,
                        UnImportedNum = importHistory.UnImportRows,
                        LastImportedId = _importHistoryService.LastHistory().EntityId,
                    };

                    return result;
                }
                catch (Exception ex)
                {
                    //
                    // 数据导入异常信息
                    //
                    importHistory.Exception = ex.Message;
                    _importHistoryService.Update(importHistory);
                    result.Data = new
                    {
                        type = 1,
                        msg = "导入数据异常！" + ex.Message,
                        LastImportedId = _importHistoryService.LastHistory().EntityId,
                    };
                    return result;
                }
            }
        }
        #endregion

        #region 动态导出
        public ActionResult DynamicDataExport()
        {
            return View(new ImportResultModel { });
        }

        [Permission(EnumBusinessPermission.SycData_Syc_Export)]
        public ActionResult Export(string type = "")
        {
            switch (type.ToUpper())
            {
                case "LAND":
                    {
                        try
                        {
                            var landExport = new ExportLand(_assetsService);
                            landExport.Loaddata();
                            if (landExport.GetPagecount() == 1)
                            {
                                landExport.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "bookExport"));
                                landExport.Save(savePath);
                                return File(savePath, "application/ms-excel", landExport.SaveFileName);
                            }
                            else
                            {
                                ExportDiy<ExportLand> ed = new ExportDiy<ExportLand>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(landExport, filename);
                                return File(Server.MapPath("~/Temp/bookExport.zip"), "application/zip", "bookExport.zip");
                            }
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }

                    }
                case "HOUSE":
                    {
                        try
                        {

                            var houseExport = new ExportHouse(_assetsService);
                            houseExport.Loaddata();
                            if (houseExport.GetPagecount() == 1)
                            {
                                houseExport.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "houseExport"));
                                houseExport.Save(savePath);
                                return File(savePath, "application/ms-excel", houseExport.SaveFileName);
                            }
                            else
                            {
                                ExportDiy<ExportHouse> ed = new ExportDiy<ExportHouse>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(houseExport, filename);
                                return File(Server.MapPath("~/Temp/houseExport.zip"), "application/zip", "houseExport.zip");
                            }
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }

                    }
                case "BUILDING":
                    {
                        try
                        {

                            var BUILDING = new ExportBuild(_assetsService);
                            BUILDING.Loaddata();
                            if (BUILDING.GetPagecount() == 1)
                            {
                                BUILDING.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "BUILDING"));
                                BUILDING.Save(savePath);
                                return File(savePath, "application/ms-excel", BUILDING.SaveFileName);
                            }
                            else
                            {
                                ExportDiy<ExportBuild> ed = new ExportDiy<ExportBuild>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(BUILDING, filename);
                                return File(Server.MapPath("~/Temp/BUILDING.zip"), "application/zip", "BUILDING.zip");
                            }
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }
                        break;
                    }
                case "CAR":
                    {
                        try
                        {

                            var CAR = new ExportCar(_assetsService);
                            CAR.Loaddata();
                            if (CAR.GetPagecount() == 1)
                            {
                                CAR.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "CAR"));
                                CAR.Save(savePath);
                                return File(savePath, "application/ms-excel", CAR.SaveFileName);
                            }
                            else
                            {
                                ExportDiy<ExportCar> ed = new ExportDiy<ExportCar>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(CAR, filename);
                                return File(Server.MapPath("~/Temp/CAR.zip"), "application/zip", "CAR.zip");
                            }
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }
                        break;
                    }
                case "GENERAL":
                    {
                        try
                        {

                            var GENERAL = new ExportGeneral(_assetsService);
                            GENERAL.Loaddata();
                            if (GENERAL.GetPagecount() == 1)
                            {
                                GENERAL.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "GENERAL"));
                                GENERAL.Save(savePath);
                                return File(savePath, "application/ms-excel", GENERAL.SaveFileName);
                            }
                            else
                            {
                                ExportDiy<ExportGeneral> ed = new ExportDiy<ExportGeneral>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(GENERAL, filename);
                                return File(Server.MapPath("~/Temp/GENERAL.zip"), "application/zip", "GENERAL.zip");
                            }
                           
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }
                        break;
                    }
                case "SPECIAL":
                    {
                        try
                        {
                            var SPECIAL = new ExportSpecial(_assetsService);
                            SPECIAL.Loaddata();
                            if (SPECIAL.GetPagecount() == 1)
                            {
                                SPECIAL.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "SPECIAL"));
                                SPECIAL.Save(savePath);
                                return File(savePath, "application/ms-excel", SPECIAL.SaveFileName);
                            }
                            else {
                                ExportDiy<ExportSpecial> ed = new ExportDiy<ExportSpecial>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(SPECIAL, filename);
                                return File(Server.MapPath("~/Temp/special.zip"), "application/zip", "special.zip");                              
                            }
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }
                        break;
                    }
                case "CULTURALRELIC":
                    {
                        try
                        {
                            var CULTURALRELIC = new ExportCulturalrelic(_assetsService);
                            CULTURALRELIC.Loaddata();
                            if (CULTURALRELIC.GetPagecount() == 1)
                            {
                                CULTURALRELIC.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "CULTURALRELIC"));
                                CULTURALRELIC.Save(savePath);
                                return File(savePath, "application/ms-excel", CULTURALRELIC.SaveFileName);
                            }
                            else
                            {
                                ExportDiy<ExportCulturalrelic> ed = new ExportDiy<ExportCulturalrelic>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(CULTURALRELIC, filename);
                                return File(Server.MapPath("~/Temp/CULTURALRELIC.zip"), "application/zip", "CULTURALRELIC.zip");
                            }
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }

                    }
                case "FURNITURE":
                    {
                        try
                        {
                            var FURNITURE = new ExportFurniture(_assetsService);
                            FURNITURE.Loaddata();
                            if (FURNITURE.GetPagecount() == 1)
                            {
                                FURNITURE.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "FURNITURE"));
                                FURNITURE.Save(savePath);
                                return File(savePath, "application/ms-excel", FURNITURE.SaveFileName);
                            }
                            else
                            {
                                ExportDiy<ExportFurniture> ed = new ExportDiy<ExportFurniture>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(FURNITURE, filename);
                                return File(Server.MapPath("~/Temp/FURNITURE.zip"), "application/zip", "FURNITURE.zip");
                            }
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }

                    }
                case "BOOK":
                    {
                        try
                        {
                            var bookExport = new ExportBook(_assetsService);
                            bookExport.Loaddata();
                            if (bookExport.GetPagecount() == 1)
                            {
                                bookExport.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "bookExport"));
                                bookExport.Save(savePath);
                                return File(savePath, "application/ms-excel", bookExport.SaveFileName);
                            }
                            else
                            {
                                ExportDiy<ExportBook> ed = new ExportDiy<ExportBook>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(bookExport, filename);
                                return File(Server.MapPath("~/Temp/bookExport.zip"), "application/zip", "bookExport.zip");
                            }
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }

                    }
                case "ANIMALANDPLANT":
                    {
                        try
                        {
                            var ANIMALANDPLANT = new ExportAnimalandplant(_assetsService);
                            ANIMALANDPLANT.Loaddata();
                            if (ANIMALANDPLANT.GetPagecount() == 1)
                            {
                                ANIMALANDPLANT.DoExport();
                                var tempFile = Server.MapPath("~/Temp/");
                                var savePath = Path.Combine(tempFile,
                                    string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "ANIMALANDPLANT"));
                                ANIMALANDPLANT.Save(savePath);
                                return File(savePath, "application/ms-excel", ANIMALANDPLANT.SaveFileName);
                            }
                            else
                            {
                                ExportDiy<ExportAnimalandplant> ed = new ExportDiy<ExportAnimalandplant>();
                                string filename = Server.MapPath("~/Temp/");
                                ed.MultiExport(ANIMALANDPLANT, filename);
                                return File(Server.MapPath("~/Temp/ANIMALANDPLANT.zip"), "application/zip", "ANIMALANDPLANT.zip");
                            }
                        }
                        catch (Exception ex)
                        {
                            return Back(ex.Message);
                        }

                    }
                default:
                    return Back("类型错误");

            }

        }

        #endregion

        #region 模板下载
        [Permission(EnumBusinessPermission.SycData_Syc_Export)]
        public ActionResult DownLoadBxSample()
        {
            var tempFile = Server.MapPath("~/Temp/");
            var savePath = Path.Combine(tempFile,
                string.Format("{0}.xls", "办学导入模板"));
            return File(savePath, "application/ms-excel", "办学导入模板.xls");
        }

        [Permission(EnumBusinessPermission.SycData_Syc_Export)]
        public ActionResult DownLoadDtSample()
        {
            var tempFile = Server.MapPath("~/Temp/");
            var savePath = Path.Combine(tempFile,
                string.Format("{0}.xls", "动态导入模板"));
            return File(savePath, "application/ms-excel", "动态导入模板.xls");
        }
        [Permission(EnumBusinessPermission.SycData_Syc_Export)]
        public ActionResult DownLoadBfSample()
        {
            var tempFile = Server.MapPath("~/Temp/");
            var savePath = Path.Combine(tempFile,
                string.Format("{0}.xls", "报废导入模板"));
            return File(savePath, "application/ms-excel", "报废导入模板.xls");
        }
        /// <summary>
        /// 盘盈导入 2017-05-26 wnn
        /// </summary>
        /// <returns></returns>
        [Permission(EnumBusinessPermission.SycData_Syc_Export)]
        public ActionResult DownLoadPSample()
        {
            var tempFile = Server.MapPath("~/Temp/");
            var savePath = Path.Combine(tempFile,
                string.Format("{0}.xls", "盘盈导入模板"));
            return File(savePath, "application/ms-excel", "盘盈导入模板.xls");
        }
        #endregion

        [Permission(EnumBusinessPermission.SycData_Syc_Diff)]
        public ActionResult Diff()
        {
            var model = new DiffModel
            {
                LastBanxue = _importHistoryService.LastHistory(1) ?? new ImportHistory(),
                LastDynamic = _importHistoryService.LastHistory(2) ?? new ImportHistory()
            };
            return View(model);
        }


        //
        // 数据比对
        //
        [Permission(EnumBusinessPermission.SycData_Syc_Diff)]
        [HttpPost]
        public ActionResult Diff(string banxueFile, string dynamicFile, string DiffBase)
        {
            var model = new DiffModel();

            if (!string.IsNullOrEmpty(banxueFile))
                banxueFile = Path.Combine(PortalSetting.UpLoadTempPath, banxueFile);
            if (!string.IsNullOrEmpty(dynamicFile))
                dynamicFile = Path.Combine(PortalSetting.UpLoadTempPath, dynamicFile);
            var diffExecuter = new DiffExecuter(_assetsService, _importHistoryService, _unImportAssetsService);
            switch (DiffBase)
            {
                case "本库":
                    try
                    {
                        var LocaldiffItems = diffExecuter.Execute(banxueFile, dynamicFile, "本库");
                    }
                    catch (Exception ex)
                    {
                        model.ErrMessage = ex.Message;
                    }
                    break;
                case "办学库":
                    try
                    {
                        var BxdiffItems = diffExecuter.Execute(banxueFile, dynamicFile, "办学库");
                    }
                    catch (Exception ex)
                    {
                        model.ErrMessage = ex.Message;
                    }
                    break;
                case "动态库":
                    try
                    {
                        var DtdiffItems = diffExecuter.Execute(banxueFile, dynamicFile, "动态库");
                    }
                    catch (Exception ex)
                    {
                        model.ErrMessage = ex.Message;
                    }
                    break;
                default:
                    break;
            }

            //if(!string.IsNullOrEmpty(banxueFile))
            //    banxueFile = Path.Combine(PortalSetting.UpLoadTempPath, banxueFile);
            //if (!string.IsNullOrEmpty(dynamicFile))
            //    dynamicFile = Path.Combine(PortalSetting.UpLoadTempPath, dynamicFile); 
            //var diffExecuter = new DiffExecuter(_assetsService);
            //var diffItems = diffExecuter.Execute(banxueFile, dynamicFile);
            model.LastBanxue = _importHistoryService.LastHistory(1);
            model.LastDynamic = _importHistoryService.LastHistory(2);
            //model.DataDiffs = diffItems ?? new List<DiffItem>();

            if (null == model.LastBanxue)
                model.LastBanxue = new ImportHistory();
            if (null == model.LastDynamic)
                model.LastDynamic = new ImportHistory();
            if (null == model.LastDynamic)
                model.DataDiffs = new List<DiffItem>();

            return View(model);
        }

        /// <summary>
        /// 比对结果查询
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public JsonResult DiffResultQuery(AllRecordQuery model)
        {

            JsonResult ret = new JsonResult();
            ProgressInfo progressInfo = new ProgressInfo();
            var taskId = Guid.NewGuid();
            ImportTasks.Add(taskId, progressInfo);
            //为了实现前端分页效果 需要将后台的数据一次性读取到前端 所以这里将一页的Size设置为最大
            model.PageSize = int.MaxValue;
            //model.PageSize = 100;
            var result = _assetsService.QueryPage(model);
            return Json(result);
        }

        /// <summary>
        /// 下载比对结果
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        //[HttpPost]
        public ActionResult DownLoadDiffResult(AssetsQuery model)
        {

            //
            // 写到文件中
            //
            var diffResult = new ExportDiffResult(_assetsService);
            diffResult.DoExport();
            string tempFile = Server.MapPath("~/Temp/");
            var savePath = Path.Combine(tempFile, string.Format("{0}.xls", DateTime.Now.ToString("yyMMddHHmmss") + "LAND"));
            diffResult.Save(savePath);
            return File(savePath, "application/ms-excel", diffResult.SaveFileName);
        }

        /// <summary>
        /// 删除某一条动态导入记录
        /// 并且删除该条记录所有导入的资产
        /// </summary>
        /// <param name="recordid"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult DeleteQuery(int recordid)
        {
            if (recordid > 0)
            {
                //
                // 调用服务删除记录和资产
                //
                if (_importHistoryService.DeleteRecordAndData(recordid) >= 1)
                    return Back("删除成功!");
            }

            return Back("删除失败!");
        }
        /// <summary>
        /// 人员导入  2017-05-25 wnn
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public JsonResult UserDataInput(HttpPostedFileBase file)
        {
            {
                //
                // 导入过程信息定义
                //
                ProgressInfo progressInfo = new ProgressInfo();
                //progressInfo.ImportedPercentVal = 1;
                JsonResult result = new JsonResult();

                //
                // 导入文件判空
                //
                if (file == null)
                {
                    result.Data = new { type = 1, msg = "请选择导入文件！" };
                    return result;
                }

                //
                // 将导入文件上传到服务器
                //
                var fileName = string.Format("人员_{0}{1}", DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Path.GetExtension(file.FileName));
                var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);

                //
                // 导入历史
                //
                ImportHistory importHistory;
                try
                {
                    progressInfo.ImportedPercentVal = 2;
                    file.SaveAs(saveFileName);
                    progressInfo.ImportedPercentVal = 3;
                    importHistory = new ImportHistory
                    {
                        ImportFile = fileName,
                        ImportTime = DateTime.Now,
                        ImportType = 2,
                        UserId = base.UserId
                    };
                    //importHistory = _importHistoryService.Add(importHistory);
                }
                catch (Exception ex)
                {

                    result.Data = new { type = 1, msg = "上传文件异常！" + ex };
                    return result;
                }
                var taskId = Guid.NewGuid();
                ImportTasks.Add(taskId, progressInfo);
                importHistory = _importHistoryService.Add(importHistory);

                //
                // 将导入文件写入到数据库
                //
                try
                {
                    //
                    // 将导入文件数据读取出来
                    //
                    var dtImport = new DtUImport(_userService, _importHistoryService, _unImportAssetsService, _roleService);

                    //
                    // 将提取的数据条目写入到数据库
                    //
                    var result1 = dtImport.DoUImport(saveFileName, ref progressInfo, _importHistoryService, _unImportAssetsService, _roleService);

                    //
                    // 更新数据导入历史

                    importHistory.UnImportRows = progressInfo.TotalAssetsNum - progressInfo.ImportedAssetsNum;
                    importHistory.ImportRows = progressInfo.ImportedAssetsNum;
                    importHistory.TotalRows = progressInfo.TotalAssetsNum;
                    _importHistoryService.Update(importHistory);

                    //
                    // 导入结果填写并返回
                    //
                    result.Data = new
                    {
                        type = 0,
                        //ImportedItems = result1.Imported,
                        //  UnImportedItems = result1.UnImported,  //！！！不能用
                        ImportedNum = importHistory.ImportRows,
                        UnImportedNum = importHistory.UnImportRows,
                        LastImportedId = _importHistoryService.LastHistory().EntityId,
                    };

                    return result;
                }
                catch (Exception ex)
                {
                    //
                    // 数据导入异常信息
                    //
                    importHistory.Exception = ex.Message;
                    _importHistoryService.Update(importHistory);
                    result.Data = new
                    {
                        type = 1,
                        msg = "导入数据异常！" + ex.Message,
                        LastImportedId = _importHistoryService.LastHistory().EntityId,
                    };
                    return result;
                }
            }
        }


        /// <summary>
        /// 位置导入  2017-06-02 wnn
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public JsonResult PlaceDataInput(HttpPostedFileBase file)
        {
            {
                //
                // 导入过程信息定义
                //
                ProgressInfo progressInfo = new ProgressInfo();
                //progressInfo.ImportedPercentVal = 1;
                JsonResult result = new JsonResult();

                //
                // 导入文件判空
                //
                if (file == null)
                {
                    result.Data = new { type = 1, msg = "请选择导入文件！" };
                    return result;
                }

                //
                // 将导入文件上传到服务器
                //
                var fileName = string.Format("位置_{0}{1}", DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Path.GetExtension(file.FileName));
                var saveFileName = Path.Combine(PortalSetting.UpLoadTempPath, fileName);

                //
                // 导入历史
                //
                ImportHistory importHistory;
                try
                {
                    progressInfo.ImportedPercentVal = 2;
                    file.SaveAs(saveFileName);
                    progressInfo.ImportedPercentVal = 3;
                    importHistory = new ImportHistory
                    {
                        ImportFile = fileName,
                        ImportTime = DateTime.Now,
                        ImportType = 2,
                        UserId = base.UserId
                    };
                    //importHistory = _importHistoryService.Add(importHistory);
                }
                catch (Exception ex)
                {

                    result.Data = new { type = 1, msg = "上传文件异常！" + ex };
                    return result;
                }
                var taskId = Guid.NewGuid();
                ImportTasks.Add(taskId, progressInfo);
                importHistory = _importHistoryService.Add(importHistory);

                //
                // 将导入文件写入到数据库
                //
                try
                {
                    //
                    // 将导入文件数据读取出来
                    //
                    var dtImport = new DtPImport(_sysService, _importHistoryService, _unImportAssetsService, _roleService);

                    //
                    // 将提取的数据条目写入到数据库
                    //
                    var result1 = dtImport.DoPImport(saveFileName, ref progressInfo, _importHistoryService, _unImportAssetsService,_sysService);

                    //
                    // 更新数据导入历史

                    importHistory.UnImportRows = progressInfo.TotalAssetsNum - progressInfo.ImportedAssetsNum;
                    importHistory.ImportRows = progressInfo.ImportedAssetsNum;
                    importHistory.TotalRows = progressInfo.TotalAssetsNum;
                    _importHistoryService.Update(importHistory);

                    //
                    // 导入结果填写并返回
                    //
                    result.Data = new
                    {
                        type = 0,
                        //ImportedItems = result1.Imported,
                        //  UnImportedItems = result1.UnImported,  //！！！不能用
                        ImportedNum = importHistory.ImportRows,
                        UnImportedNum = importHistory.UnImportRows,
                        LastImportedId = _importHistoryService.LastHistory().EntityId,
                    };

                    return result;
                }
                catch (Exception ex)
                {
                    //
                    // 数据导入异常信息
                    //
                    importHistory.Exception = ex.Message;
                    _importHistoryService.Update(importHistory);
                    result.Data = new
                    {
                        type = 1,
                        msg = "导入数据异常！" + ex.Message,
                        LastImportedId = _importHistoryService.LastHistory().EntityId,
                    };
                    return result;
                }
            }
        }
    }
}