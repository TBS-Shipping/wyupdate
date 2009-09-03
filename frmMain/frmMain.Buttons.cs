﻿using System;
using System.Windows.Forms;
using wyUpdate.Common;

namespace wyUpdate
{
    public partial class frmMain
    {
        private void btnNext_Click(object sender, EventArgs e)
        {
            if (FrameIs.ErrorFinish(frameOn))
            {
                Close();
            }
            else
            {
                if (needElevation || willSelfUpdate)
                    StartSelfElevated();
                else
                    ShowFrame(frameOn + 1);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            CancelUpdate();
        }


        private void CancelUpdate(bool ForceClose)
        {
            if ((frameOn == Frame.Checking || frameOn == Frame.InstallUpdates) && !ForceClose) //if downloading or updating
            {
                DialogResult dResult = MessageBox.Show(clientLang.CancelDialog.Content, clientLang.CancelDialog.Title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);

                if (dResult == DialogResult.Yes)
                {
                    //cancel the update
                    isCancelled = true;
                    if (IsDownloading())
                    {
                        if (downloader != null)
                            downloader.Cancel(); //cancel any downloads

                        //TODO: We should give the 'downloader' a bit of time to clean up partial files

                        //Bail out quickly. Don't hang around for servers to lazily respond.
                        isCancelled = true;
                        Close();
                        return;
                    }

                    if (frameOn == Frame.InstallUpdates && !IsDownloading())
                        installUpdate.Cancel(); //cancel updates

                    //disable the 'X' button & cancel button
                    DisableCancel();
                } //otherwise, do nothing
            }
            else
            {
                //either force closed, or not download/updating
                isCancelled = true;
                Close();
            }
        }

        private bool IsDownloading()
        {
            //if downloading in anything, return true
            return frameOn == Frame.Checking || frameOn == Frame.InstallUpdates && downloader != null &&
                (update.CurrentlyUpdating == UpdateOn.DownloadingUpdate || update.CurrentlyUpdating == UpdateOn.DownloadingClientUpdt);
        }


        private void CancelUpdate()
        {
            CancelUpdate(false);
        }

        private void DisableCancel()
        {
            if (btnCancel.Enabled)
                SystemMenu.DisableCloseButton(this);

            btnCancel.Enabled = false;
        }

        private void EnableCancel()
        {
            if (!btnCancel.Enabled)
                SystemMenu.EnableCloseButton(this);

            btnCancel.Enabled = true;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            // handle the effect of minimize/restore on 
            // disabling the "close" button & menu item
            if (!btnCancel.Enabled)
                SystemMenu.DisableCloseButton(this);

            base.OnSizeChanged(e);
        }

        private void SetButtonText()
        {
            btnNext.Text = clientLang.NextButton;
            btnCancel.Text = clientLang.CancelButton;
        }

        private void btnCancel_SizeChanged(object sender, EventArgs e)
        {
            btnNext.Left = btnCancel.Left - btnNext.Width - 6;
        }
    }
}