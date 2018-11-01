﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FluentValidation.Results;
using Newtonsoft.Json;
using SWLOR.Game.Server.Data;
using SWLOR.Game.Server.Data.Contracts;
using SWLOR.Game.Server.Data.Processor;
using SWLOR.Game.Server.Extension;
using SWLOR.Game.Server.Service.Contracts;
using SWLOR.Game.Server.ValueObject;

namespace SWLOR.Game.Server.Service
{
    public class DataPackageService : IDataPackageService
    {
        private readonly IDataContext _db;
        
        public DataPackageService(IDataContext db)
        {
            _db = db;
        }

        public void OnModuleLoad()
        {
            const string PackagesPath = "./DataPackages/";
            
            // Look for an existing DataPackages folder. If it's missing, create it.
            if(!Directory.Exists(PackagesPath))
            {
                Directory.CreateDirectory(PackagesPath);
            }

            // Pull back all of the packages we've already attempted to import.
            var importedPackages = _db.DataPackages.ToList();

            // Enumerate all packages in the directory.
            // We will process these later in order by their export date.
            // In other words, processing occurs from oldest to newest.
            string[] files = Directory.GetFiles(PackagesPath, "*.json");
            foreach (var file in files)
            {
                string checksum;

                using (var sha1 = SHA1.Create())
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var hash = sha1.ComputeHash(stream);
                        checksum = BitConverter.ToString(hash).Replace("-", "").ToLower();
                    }
                }
                
                // If a file with this checksum has already been imported, move to the next one.
                if (importedPackages.FirstOrDefault(x => x.Checksum == checksum) != null)
                    continue;
                
                DataPackage package = new DataPackage
                {
                    Checksum = checksum,
                    DateFound = DateTime.UtcNow,
                    FileName = Path.GetFileName(file),
                    ErrorMessage = string.Empty
                };

                try
                {
                    package.Content = File.ReadAllText(file);
                    DataPackageFile dpf = JsonConvert.DeserializeObject<DataPackageFile>(package.Content);
                    package.DateExported = dpf.ExportDate;
                    package.PackageName = dpf.PackageName;

                    string processingErrors = ProcessDataPackageFile(dpf);

                    if (!string.IsNullOrWhiteSpace(processingErrors))
                    {
                        package.ErrorMessage = processingErrors;
                    }
                    else
                    {
                        package.ImportedSuccessfully = true;
                    }
                }
                catch(Exception ex)
                {
                    package.ErrorMessage = ex.ToMessageAndCompleteStacktrace();
                    package.ImportedSuccessfully = false;   
                }

                _db.DataPackages.Add(package);
                _db.SaveChanges();

                if (package.ImportedSuccessfully)
                {
                    Console.WriteLine("Processed package " + package.PackageName + " successfully.");
                }
                else
                {
                    File.WriteAllText("IMPORT_FAILURE_" + DateTime.UtcNow.ToString("yyyy-dd-M--HH-mm-ss") + ".log", package.ErrorMessage);
                    Console.WriteLine("FAILURE: Package " + package.PackageName + " failed to import. Check the logs for errors.");
                }
            }
        }

        private string ProcessDataPackageFile(DataPackageFile dpf)
        {
            string errors = string.Empty;
            App.Resolve<IDataContext>(db =>
            {
                foreach (var obj in dpf.ApartmentBuildings)
                    errors += ValidateAndProcess(db, new ApartmentBuildingProcessor(), obj) + "\n";
                foreach (var obj in dpf.BaseStructures)
                    errors += ValidateAndProcess(db, new BaseStructureProcessor(), obj) + "\n";
                foreach (var obj in dpf.BuildingStyles)
                    errors += ValidateAndProcess(db, new BuildingStyleProcessor(), obj) + "\n";
                foreach (var obj in dpf.CooldownCategories)
                    errors += ValidateAndProcess(db, new CooldownCategoryProcessor(), obj) + "\n";
                foreach (var obj in dpf.CraftBlueprintCategories)
                    errors += ValidateAndProcess(db, new CraftBlueprintCategoryProcessor(), obj) + "\n";
                foreach (var obj in dpf.CraftBlueprints)
                    errors += ValidateAndProcess(db, new CraftBlueprintProcessor(), obj) + "\n";
                foreach (var obj in dpf.CraftDevices)
                    errors += ValidateAndProcess(db, new CraftDeviceProcessor(), obj) + "\n";
                foreach (var obj in dpf.CustomEffects)
                    errors += ValidateAndProcess(db, new CustomEffectProcessor(), obj) + "\n";
                foreach (var obj in dpf.Downloads)
                    errors += ValidateAndProcess(db, new DownloadProcessor(), obj) + "\n";
                foreach (var obj in dpf.FameRegions)
                    errors += ValidateAndProcess(db, new FameRegionProcessor(), obj) + "\n";
                foreach (var obj in dpf.GameTopicCategories)
                    errors += ValidateAndProcess(db, new GameTopicCategoryProcessor(), obj) + "\n";
                foreach (var obj in dpf.GameTopics)
                    errors += ValidateAndProcess(db, new GameTopicProcessor(), obj) + "\n";
                foreach (var obj in dpf.KeyItemCategories)
                    errors += ValidateAndProcess(db, new KeyItemCategoryProcessor(), obj) + "\n";
                foreach (var obj in dpf.KeyItems)
                    errors += ValidateAndProcess(db, new KeyItemProcessor(), obj) + "\n";
                foreach (var obj in dpf.LootTableItems)
                    errors += ValidateAndProcess(db, new LootTableItemProcessor(), obj) + "\n";
                foreach (var obj in dpf.LootTables)
                    errors += ValidateAndProcess(db, new LootTableProcessor(), obj) + "\n";
                foreach (var obj in dpf.Mods)
                    errors += ValidateAndProcess(db, new ModProcessor(), obj) + "\n";
                foreach (var obj in dpf.NPCGroups)
                    errors += ValidateAndProcess(db, new NPCGroupProcessor(), obj) + "\n";
                foreach (var obj in dpf.PerkCategories)
                    errors += ValidateAndProcess(db, new PerkCategoryProcessor(), obj) + "\n";
                foreach (var obj in dpf.Plants)
                    errors += ValidateAndProcess(db, new PlantProcessor(), obj) + "\n";
                foreach (var obj in dpf.Quests)
                    errors += ValidateAndProcess(db, new QuestProcessor(), obj) + "\n";
                foreach (var obj in dpf.SkillCategories)
                    errors += ValidateAndProcess(db, new SkillCategoryProcessor(), obj) + "\n";
                foreach (var obj in dpf.Skills)
                    errors += ValidateAndProcess(db, new SkillProcessor(), obj) + "\n";
                foreach (var obj in dpf.Spawns)
                    errors += ValidateAndProcess(db, new SpawnProcessor(), obj) + "\n";
                
                // Nothing in the package gets committed to the database if any error occurs.
                if (string.IsNullOrWhiteSpace(errors))
                {
                    db.SaveChanges();
                }
            });

            return errors;
        }
        
        private string ValidateAndProcess<T>(IDataContext db, IDataProcessor<T> processor, T dataObject)
        {
            string errors = string.Empty;
            
                ValidationResult validationResult = processor.Validator.Validate(dataObject);

                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        errors += error.ErrorMessage + "\n";
                    }
                }
                else
                {
                    try
                    {
                        processor.Process(db, dataObject);
                    }
                    catch (Exception ex)
                    {
                        errors += "Failed to process object of type: " + dataObject.GetType() + " Reason: " + ex.ToMessageAndCompleteStacktrace();
                    }
                };

            return errors;
        }

    }
}