using NUnit.Framework;
using StormEngine.Core;

namespace StormEngine.Tests
{
    public class NameFilterTests
    {
        [TestCase("fuck")]
        [TestCase("FUCK_YOU")]
        [TestCase("f_u_c_k")]
        [TestCase("Sh-it")]
        [TestCase("fuuuuck")]
        [TestCase("5h1t")]
        [TestCase("Sh!t")]
        [TestCase("BigAss")]
        [TestCase("big_ass")]
        [TestCase("A55")]
        [TestCase("Asssss")]
        [TestCase("Cock99")]
        [TestCase("N1gg3r")]
        [TestCase("Hitler88")]
        [TestCase("Fuck#1234")]
        [TestCase("puta_madre")]
        [TestCase("Scheiße")]
        [TestCase("kurwa_mac")]
        [TestCase("Orospu_Cocugu")]
        [TestCase("Сука")]
        [TestCase("хуй")]
        [TestCase("địt_mẹ")]
        [TestCase("Cặc")]
        [TestCase("đụ_má")]
        [TestCase("เย็ด")]
        public void BlocksOffensiveNames(string name) => Assert.IsFalse(NameFilter.IsAllowed(name), name);

        // Innocent names that simple filters get wrong.
        [TestCase("Storm_Chaser#1234")]
        [TestCase("Classic_Gamer")]
        [TestCase("Assassin")]
        [TestCase("Peacock")]
        [TestCase("Hancock")]
        [TestCase("Cockpit")]
        [TestCase("Dickens")]
        [TestCase("Grape_Juice")]
        [TestCase("Therapist")]
        [TestCase("Cumulus")]
        [TestCase("Cumulonimbus")]
        [TestCase("Titanic")]
        [TestCase("Essex")]
        [TestCase("Pen_Island")]
        [TestCase("Nigeria")]
        [TestCase("Niger")]
        [TestCase("Nazir")]
        [TestCase("Merdeka")]
        [TestCase("Merdan")]
        [TestCase("Computadora")]
        [TestCase("Reputation")]
        [TestCase("Torpedo")]
        [TestCase("Figaro")]
        [TestCase("Swanky")]
        [TestCase("Atwater")]
        [TestCase("Pussycat")]
        [TestCase("Booboo")]
        [TestCase("Pornchai")]
        [TestCase("Anticipation")]
        [TestCase("Chatterbox")]
        [TestCase("Pollard")]
        [TestCase("Vergara")]
        [TestCase("Bostanci")]
        [TestCase("Nutter")]
        [TestCase("Amina")]
        [TestCase("Hamka")]
        [TestCase("Sikandar")]
        [TestCase("Pępek")]
        [TestCase("Корабль")]
        [TestCase("Глеб")]
        [TestCase("Гроза")]
        [TestCase("Đụng_Độ")]
        [TestCase("Đĩa_Bay")]
        [TestCase("Bão_Tố")]
        [TestCase("พายุ")]
        [TestCase("Fırtına")]
        [TestCase("Tempête")]
        [TestCase("Gewitter")]
        [TestCase("Xx_Thunder_99")]
        [TestCase("")]
        [TestCase((string)null)]
        public void AllowsInnocentNames(string name) => Assert.IsTrue(NameFilter.IsAllowed(name), name);

        [Test]
        public void DisplayFallsBackForBlockedNames()
        {
            Assert.AreEqual("Storm_Chaser#1234", NameFilter.Display("Storm_Chaser#1234", "Storm Chaser"));
            Assert.AreEqual("Storm Chaser", NameFilter.Display("Shit_Storm#77", "Storm Chaser"));
        }
    }
}
