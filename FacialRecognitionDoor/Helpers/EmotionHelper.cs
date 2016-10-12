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

        public static async Task<Tuple<Microsoft.ProjectOxford.Emotion.Contract.Emotion, Microsoft.ProjectOxford.Face.Contract.FaceAttributes>> DetectEmotions(StorageFile imageFile)
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

            var face = faces.First();
            var faceRectangle = new Microsoft.ProjectOxford.Common.Rectangle()
            {
                Height = face.FaceRectangle.Height,
                Left = face.FaceRectangle.Left,
                Top = face.FaceRectangle.Top,
                Width = face.FaceRectangle.Top
            };

            var emotionResult = await EmotionRecognittion.EmotionApi.Instance.RecognizeEmotions(imageFile, new Microsoft.ProjectOxford.Common.Rectangle[] { faceRectangle });

            return new Tuple<Microsoft.ProjectOxford.Emotion.Contract.Emotion, Microsoft.ProjectOxford.Face.Contract.FaceAttributes>(emotionResult.First(), face.FaceAttributes);
        }
    }
}
