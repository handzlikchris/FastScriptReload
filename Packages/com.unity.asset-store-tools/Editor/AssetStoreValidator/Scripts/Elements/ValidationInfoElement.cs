using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Validator
{
    internal class ValidationInfoElement : VisualElement
    {
        private const string GuidelinesUrl = "https://assetstore.unity.com/publishing/submission-guidelines#Overview";
        private const string SupportUrl = "https://support.unity.com/hc/en-us/requests/new?ticket_form_id=65905";
        
        public ValidationInfoElement()
        {
            ConstructInformationElement();
        }

        private void ConstructInformationElement()
        {
            AddToClassList("validation-info-box");
            
            var scanLabel = new Label
            {
                text = "Validate your package to ensure content meets a consistent 'Product Content' standard. " +
                       "Passing this scan does not guarantee that your package will get accepted as the final " +
                       "decision is made by the Unity Asset Store team."
            };
            scanLabel.AddToClassList("scan-label");

            var uploadPackageLabel = new Label
            {
                text = "The tests are not obligatory for submitting your assets, but they will help to avoid instant rejections. " +
                       "For more information, view the message next to the test in the checklist or contact our support team."
            };
            uploadPackageLabel.AddToClassList("upload-package-label");

            var submissionGuidelinesButton = new Button(() => OpenURL(GuidelinesUrl))
            {
                name = "GuidelinesButton",
                text = "Submission guidelines"
            };
            
            submissionGuidelinesButton.AddToClassList("hyperlink-button");
            
            var supportTicketButton = new Button(() => OpenURL(SupportUrl))
            {
                name = "SupportTicket",
                text = "Contact our support team"
            };
            
            supportTicketButton.AddToClassList("hyperlink-button");

            Add(scanLabel);
            Add(uploadPackageLabel);
            Add(submissionGuidelinesButton);
            Add(supportTicketButton);
        }

        private void OpenURL(string url)
        {
            Application.OpenURL(url);
        }
        
    }
}