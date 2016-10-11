using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace IotPrototype.Helpers
{
    public class EmotionHelper
    {

        public static async Task<Microsoft.ProjectOxford.Emotion.Contract.Emotion> DetectEmotions(StorageFile imageFile)
        {

            var faces = await FacialRecognition.FaceApiRecognizer.Instance.DetectFacesFromImage(imageFile);

            if (faces.Count() == 0)
            {
                throw new Exception("Cannot find any faces");
            }
            if (faces.Count() > 1)
            {
                throw new Exception("More than one cases detected");
            }


            Microsoft.ProjectOxford.Common.Rectangle faceRectangle = faces
                .Select(x=> new Microsoft.ProjectOxford.Common.Rectangle() { Height = x.FaceRectangle.Height , Left = x.FaceRectangle.Left, Top = x.FaceRectangle.Top, Width = x.FaceRectangle.Top })
                .First();

            var emotionResult = await EmotionRecognittion.EmotionApi.Instance.RecognizeEmotions(imageFile, new Microsoft.ProjectOxford.Common.Rectangle[] { faceRectangle });


            return emotionResult.First();
        }
    }
}
