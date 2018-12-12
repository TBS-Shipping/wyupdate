﻿using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using NLog;
using wyUpdate.Common;
using wyUpdate.Compression.Vcdiff;

namespace wyUpdate
{
    partial class InstallUpdate
    {
        public string ExtractPassword;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        // unzip the update to the temp folder
        public void RunUnzipProcess()
        {
            bw.DoWork += bw_DoWorkUnzip;
            bw.ProgressChanged += bw_ProgressChanged;
            bw.RunWorkerCompleted += bw_RunWorkerCompletedUnzip;

            bw.RunWorkerAsync();
        }

        void bw_DoWorkUnzip(object sender, DoWorkEventArgs e)
        {
            Exception except = null;

            string updtDetailsFilename = Path.Combine(TempDirectory, "updtdetails.udt");

            try
            {
                ExtractUpdateFile();

                try
                {
                    // remove update file (it's no longer needed)
                    File.Delete(Filename);
                }
                catch { }


                // Try to load the update details file
                if (File.Exists(updtDetailsFilename))
                {
                    UpdtDetails = UpdateDetails.Load(updtDetailsFilename);
                }
                else
                    throw new Exception("The update details file \"updtdetails.udt\" is missing.");


                if (Directory.Exists(Path.Combine(TempDirectory, "patches")))
                {
                    // patch the files
                    foreach (UpdateFile file in UpdtDetails.UpdateFiles)
                    {
                        if (file.DeltaPatchRelativePath != null)
                        {
                            if (IsCancelled())
                                break;

                            string tempFilename = Path.Combine(TempDirectory, file.RelativePath);

                            // create the directory to store the patched file
                            if (!Directory.Exists(Path.GetDirectoryName(tempFilename)))
                                Directory.CreateDirectory(Path.GetDirectoryName(tempFilename));

                            while (true)
                            {
                                try
                                {
                                    using (FileStream original = File.Open(FixUpdateDetailsPaths(file.RelativePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                    using (FileStream patch = File.Open(Path.Combine(TempDirectory, file.DeltaPatchRelativePath), FileMode.Open, FileAccess.Read, FileShare.Read))
                                    using (FileStream target = File.Open(tempFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                                    {
                                        VcdiffDecoder.Decode(original, patch, target, file.NewFileAdler32);
                                    }
                                }
                                catch (IOException IOEx)
                                {
                                    int HResult = Marshal.GetHRForException(IOEx);

                                    // if sharing violation
                                    if ((HResult & 0xFFFF) == 32)
                                    {
                                        // notify main window of sharing violation
                                        bw.ReportProgress(0, new object[] { -1, -1, string.Empty, ProgressStatus.SharingViolation, FixUpdateDetailsPaths(file.RelativePath) });

                                        // sleep for 1 second
                                        Thread.Sleep(1000);

                                        // stop waiting if cancelled
                                        if (IsCancelled())
                                            break;

                                        // retry file patch
                                        continue;
                                    }

                                    throw new PatchApplicationException("Patch failed to apply to this file: " + FixUpdateDetailsPaths(file.RelativePath) + "\r\n\r\nBecause that file failed to patch, and there's no \"catch-all\" update to download, the update failed to apply. The failure to patch usually happens because the file was modified from the original version. Reinstall the original version of this app.\r\n\r\n\r\nInternal error: " + IOEx.Message);
                                }
                                catch (Exception ex)
                                {
                                    throw new PatchApplicationException("Patch failed to apply to this file: " + FixUpdateDetailsPaths(file.RelativePath) + "\r\n\r\nBecause that file failed to patch, and there's no \"catch-all\" update to download, the update failed to apply. The failure to patch usually happens because the file was modified from the original version. Reinstall the original version of this app.\r\n\r\n\r\nInternal error: " + ex.Message);
                                }

                                // the 'last write time' of the patch file is really the 'lwt' of the dest. file
                                File.SetLastWriteTime(tempFilename, File.GetLastWriteTime(Path.Combine(TempDirectory, file.DeltaPatchRelativePath)));

                                break;
                            }
                        }
                    }


                    try
                    {
                        // remove the patches directory (frees up a bit of space)
                        Directory.Delete(Path.Combine(TempDirectory, "patches"), true);
                    }
                    catch { }
                }
            }
            /*catch (BadPasswordException ex)
            {
                except = new BadPasswordException("Could not install the encrypted update because the password did not match.");
            }*/
            catch (Exception ex)
            {
                except = ex;
            }

            if (IsCancelled() || except != null)
            {
                // report cancellation
                bw.ReportProgress(0, new object[] { -1, -1, "Cancelling update...", ProgressStatus.None, null });

                // Delete temporary files

                if (except != null && except.GetType() != typeof(PatchApplicationException))
                {
                    // remove the entire temp directory
                    try
                    {
                        Directory.Delete(OutputDirectory, true);
                    }
                    catch { }
                }
                else
                {
                    //only 'gut' the folder leaving the server file

                    string[] dirs = Directory.GetDirectories(TempDirectory);

                    foreach (string dir in dirs)
                    {
                        // delete everything but the self-update folder (AutoUpdate specific)
                        //Note: this code might be causing the "pyramid of death". TODO: Check.
                        if (Path.GetFileName(dir) == "selfupdate")
                            continue;

                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch { }
                    }

                    // remove the update details
                    if (File.Exists(updtDetailsFilename))
                    {
                        File.Delete(updtDetailsFilename);
                    }
                }

                bw.ReportProgress(0, new object[] { -1, -1, string.Empty, ProgressStatus.Failure, except });
            }
            else
            {
                bw.ReportProgress(0, new object[] { -1, -1, string.Empty, ProgressStatus.Success, null });
            }
        }

        void bw_RunWorkerCompletedUnzip(object sender, RunWorkerCompletedEventArgs e)
        {
            bw.DoWork -= bw_DoWorkUnzip;
            bw.ProgressChanged -= bw_ProgressChanged;
            bw.RunWorkerCompleted -= bw_RunWorkerCompletedUnzip;
        }

        void ExtractUpdateFile()
        {
            this.logger.Info("Extracting zip file: '{0}' to '{1}'", Filename, OutputDirectory);

            /*
            var dirInfo = new DirectoryInfo(OutputDirectory);
            this.logger.Info("Output dir atts: " + dirInfo.Attributes.ToString());
            var acl = dirInfo.GetAccessControl();
            try
            {
                this.logger.Info("ACL dump");
                this.logger.Info("RightType: {0} RuleType: {1} AccessRulesProtected: {2} AccessRulesCanonical: {3} AuditRulesCanonical: {4} AuditRulesProtected: {5}", acl.AccessRightType, acl.AccessRuleType, acl.AreAccessRulesCanonical, acl.AreAccessRulesProtected, acl.AreAuditRulesCanonical, acl.AreAuditRulesProtected);
                this.logger.Info("Access rules:");
                Type targetType = typeof(System.Security.Principal.NTAccount);

                foreach (AuthorizationRule rule in acl.GetAccessRules(true, true, targetType))
                {
                    this.logger.Info("Id: {0} Inheritance: {1} IsInherited: {2} PropagationFlags: {3}", rule.IdentityReference.Value, rule.InheritanceFlags, rule.IsInherited, rule.PropagationFlags);
                }

                this.logger.Info("Audit rules:");

                foreach (AuthorizationRule rule in acl.GetAuditRules(true, true, targetType))
                {
                    this.logger.Info("Id: {0} Inheritance: {1} IsInherited: {2} PropagationFlags: {3}", rule.IdentityReference.Value, rule.InheritanceFlags, rule.IsInherited, rule.PropagationFlags);
                }

                this.logger.Info("Sddl desc: " + acl.GetSecurityDescriptorSddlForm(AccessControlSections.All));
                this.logger.Info("Owner: {0} Group: {1}", acl.GetOwner(targetType).Value, acl.GetGroup(targetType).Value);
            }
            catch (Exception e)
            {
                this.logger.Error(e, "Couldn't get ACL");
            }*/

            using (ZipArchive zip = ZipFile.OpenRead(Filename))
            {
                int totalFiles = zip.Entries.Count;
                int filesDone = 0;

                foreach (ZipArchiveEntry e in zip.Entries)
                {
                    if (IsCancelled())
                        break; //stop outputting new files

                    if (!SkipProgressReporting)
                    {
                        int unweightedPercent = totalFiles > 0 ? (filesDone*100)/totalFiles : 0;

                        bw.ReportProgress(0, new object[] { GetRelativeProgess(1, unweightedPercent), unweightedPercent, "Extracting " + Path.GetFileName(e.Name), ProgressStatus.None, null });

                        filesDone++;
                    }

                    var outputFilePath = Path.Combine(OutputDirectory, e.FullName);
                    var outputFileDir = Path.GetDirectoryName(outputFilePath);

                    if(!string.IsNullOrEmpty(outputFileDir))
                        Directory.CreateDirectory(outputFileDir);
                    this.logger.Info("Extracting zip entry {0} to '{1}'...", e.Name, outputFilePath);
                    // if a password is provided use it to extract the updates
                    if (!string.IsNullOrEmpty(ExtractPassword))
                        //e.ExtractWithPassword(OutputDirectory, ExtractExistingFileAction.OverwriteSilently, ExtractPassword);
                        throw new Exception("Zip archives with passwords are not supported at this time");
                    else
                    {
                        try
                        {
                            e.ExtractToFile(outputFilePath, true);
                        }
                        catch (UnauthorizedAccessException exception)
                        {
                            logger.Error(exception,"Unauthorized access exception caught.");
                            throw;
                        }
                        
                    }
                }
            }
        }
    }
}