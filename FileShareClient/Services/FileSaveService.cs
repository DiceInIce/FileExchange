using System.Windows.Forms;

namespace FileShareClient.Services
{
    public class FileSaveService
    {
        public bool SaveBytes(byte[] data, string suggestedFileName)
        {
            using var dialog = new SaveFileDialog
            {
                FileName = suggestedFileName,
                Title = "Сохранить файл"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return false;
            }

            File.WriteAllBytes(dialog.FileName, data);
            return true;
        }
    }
}
