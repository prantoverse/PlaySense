using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PlaySense.Data.Models;

namespace PlaySense.EditorTools
{
    /// <summary>"PDF Report" button in the Dashboard.</summary>
    public class PdfReportAction : PlaySenseSessionAction
    {
        public override string Title
        {
            get { return "PDF Report"; }
        }

        public override int Order
        {
            get { return 50; }
        }

        public override void Run(SessionData session, string path)
        {
            string pdfPath = Path.ChangeExtension(path, ".pdf");

            try
            {
                string generated = PlaySensePDFExporter.Export(session, pdfPath);
                if (string.IsNullOrEmpty(generated))
                {
                    EditorUtility.DisplayDialog("PlaySense", "PDF generation failed. See the Console for details.", "OK");
                    return;
                }

                if (EditorUtility.DisplayDialog("PlaySense", "PDF report created:\n" + generated, "Open", "Close"))
                    EditorUtility.OpenWithDefaultApp(generated);
            }
            catch (Exception exception)
            {
                Debug.LogError("[PlaySense] PDF generation failed:\n" + exception);
            }
        }
    }
}
