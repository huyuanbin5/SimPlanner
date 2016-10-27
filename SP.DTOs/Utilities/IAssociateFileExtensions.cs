﻿using SP.DataAccess;
using SP.DataAccess.Data.Interfaces;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web;

namespace SP.Dto.Utilities
{
    public static class IAssociateFileTools
    {
        public static string GetServerPath(this IAssociateFile assocFile)
        {
            const string activityPath = @"\App_Data\TeachingResources\{0}\{1}.zip";
            const string scenarioResourcePath = @"\App_Data\Scenarios\{0}.zip"; 
            const string prereadingPath = @"\App_Data\PreReading\{0}.zip";
            Type t = assocFile.GetType();
            string returnVar;
            switch (t.Name)
            {
                case nameof(Activity):
                    var a = (Activity)assocFile;
                    returnVar = string.Format(activityPath, a.CourseActivityId, a.FileName);
                    break;
                case nameof(ActivityDto):
                    var ad = (ActivityDto)assocFile;
                    returnVar = string.Format(activityPath, ad.CourseActivityId, ad.FileName);
                    break;
                case nameof(ScenarioResource):
                    var s = (ScenarioResource)assocFile;
                    returnVar = string.Format(scenarioResourcePath, s.ScenarioId);
                    break;
                case nameof(ScenarioResourceDto):
                    var sd = (ScenarioResourceDto)assocFile;
                    returnVar = string.Format(scenarioResourcePath, sd.ScenarioId);
                    break;
                case nameof(Room):
                case nameof(RoomDto):
                    returnVar = @"\Content\images\roomMaps\" + assocFile.Id + Path.GetExtension(assocFile.FileName);
                    break;
                case nameof(Institution):
                case nameof(InstitutionDto):
                    returnVar = @"\Content\images\institutions\" + assocFile.Id + Path.GetExtension(assocFile.FileName);
                    break;
                case nameof(CandidatePrereading):
                    var c = (CandidatePrereading)assocFile;
                    returnVar = string.Format(prereadingPath, c.CourseTypeId);
                    break;
                case nameof(CandidatePrereadingDto):
                    var cd = (CandidatePrereadingDto)assocFile;
                    returnVar = string.Format(prereadingPath, cd.CourseTypeId);
                    break;
                default:
                    throw new ArgumentException("unhandled type");
            }
            return HttpRuntime.AppDomainAppPath + returnVar;
        }

        internal static IAssociateFileOptional AsOptional(this IAssociateFileRequired required)
        {
            return new AssociateFileRequiredWrapper(required);
        }

        internal static void StoreFile(this IAssociateFileRequired resource, byte[] file)
        {

            string path = GetServerPath(resource);
            CreateFile(file, resource.FileName, resource.FileModified, path);
        }

        internal static void StoreFile(this IAssociateFileOptional resource, byte[] file)
        {

            string path = GetServerPath(resource);
            CreateFile(file, resource.FileName, resource.FileModified.Value, path);
        }

        private static void CreateFile(byte[] file, string fileName, DateTime fileModified, string path)
        {
            FileInfo fi = new FileInfo(path);
            fi.Directory.Create(); // If the directory already exists, this method does nothing.
            if (path.EndsWith(FileDefaults._zipExt))
            {
                using (var stream = new MemoryStream(file, false))
                {
                    using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Update))
                    {
                        ZipArchiveEntry entry = archive.Entries.FirstOrDefault(e => e.Name == fileName)
                            ?? archive.CreateEntry(fileName, CompressionLevel.Optimal);
                        using (var arcEntryStream = entry.Open())
                        {
                            stream.CopyTo(arcEntryStream);
                        }
                        entry.LastWriteTime = fileModified;
                    }
                }
            }
            else
            {
                File.WriteAllBytes(Path.Combine(path, fileName), file);
            }

        }

        internal static void DeleteFile(this IAssociateFile resource)
        {
            var path = GetServerPath(resource);
            DeleteFile(path, resource.FileName);
        }

        private static void DeleteFile(string path, string fileName)
        {
            FileInfo fi = new FileInfo(path);
            if (!fi.Exists) { return; }
            if (path.EndsWith(FileDefaults._zipExt))
            {
                bool deleteFile = false;
                using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Update))
                {
                    ZipArchiveEntry entry = archive.Entries.FirstOrDefault(e => e.Name == fileName);
                    if (entry == null)
                    {
                        return;
                    }
                    if (archive.Entries.Count == 1)
                    {
                        deleteFile = true;
                    }
                    else
                    {
                        entry.Delete();
                    }
                }
                if (deleteFile)
                {
                    fi.Delete();
                }
            }
            else //!endwith .zip
            {
                fi.Delete();
            }
        }
    }

    internal class AssociateFileRequiredWrapper : IAssociateFileOptional
    {
        IAssociateFileRequired _assocFile;
        public AssociateFileRequiredWrapper(IAssociateFileRequired assocFile)
        {
            _assocFile = assocFile;
        }
        string IAssociateFile.FileName { get { return _assocFile.FileName;  } }
        long? IAssociateFileOptional.FileSize { get { return _assocFile.FileSize; } }
        DateTime? IAssociateFileOptional.FileModified { get { return _assocFile.FileModified; } }
        Guid IAssociateFile.Id { get { return _assocFile.Id; } }
    }  
}
