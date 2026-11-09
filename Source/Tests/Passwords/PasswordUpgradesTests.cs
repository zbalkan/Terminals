using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Configuration;
using Terminals.Data;
using Terminals.Data.Credentials;
using Terminals.Security;
using Terminals.Updates;
using Tests.Connections;
using Tests.FilePersisted;
using Tests.Helpers;

namespace Tests.Passwords
{
    /// <summary>
    /// Tests to ensure that passwords after upgrade of v2.0 work
    /// and upgrade doesn't break any stored passwords
    /// </summary>
    [TestClass]
    public class PasswordUpgradesTests
    {
        private const string EMPTY_CONFIG_FILE = "EmptyTerminals.config";
        private const string EMPTY_CREDENTIALS_FILE = "EmptyCredentials.xml";
        private const string NOMASTER_CONFIG_FILE = "NoMasterTerminals.config";
        private const string NOMASTER_CREDENTIALS_FILE = "NoMasterCredentials.xml";
        private const string SECURED_CONFIG_FILE = "SecuredTerminals.config";
        private const string SECURED_CREDENTIALS_FILE = "SecuredCredentials.xml";

        private readonly Settings settings = Settings.Instance;
        /// <summary>
        /// User name and password encrypted in test credential file
        /// </summary>
        private const string TEST_PASSWORD = "TestUser";

        public TestContext TestContext { get; set; }

        private bool askedForPassword;

        [DeploymentItem(TestDataFiles.TESTDATA_DIRECTORY + EMPTY_CONFIG_FILE)]
        [DeploymentItem(TestDataFiles.TESTDATA_DIRECTORY + EMPTY_CREDENTIALS_FILE)]
        [TestMethod]
        public void V2UpgradeEmptyConfigTest()
        {
            this.UpgradePasswordsTestInitialize(EMPTY_CONFIG_FILE, EMPTY_CREDENTIALS_FILE);
            // simply nothing to upgrade, procedure shouldn't fail.
            this.RunUpgrade();
            Assert.IsFalse(askedForPassword, "Empty config file shouldn't ask for password");
        }

        [DeploymentItem(TestDataFiles.TESTDATA_DIRECTORY + NOMASTER_CONFIG_FILE)]
        [DeploymentItem(TestDataFiles.TESTDATA_DIRECTORY + NOMASTER_CREDENTIALS_FILE)]
        [TestMethod]
        public void V2UpgradeNoMasterPasswordConfigTest()
        {
            // Do not change the committed ciphertext. It is tied to its original DPAPI user.
            string configFile;
            string credentialsFile;
            this.CopyNoMasterFixtures("OriginalNoMaster", out configFile, out credentialsFile);
            AssertOriginalNoMasterCiphertextReadable(this.CreateFullTestFileName(configFile),
                this.CreateFullTestFileName(credentialsFile));
            this.UpgradePasswordsTestInitialize(configFile, credentialsFile, "OriginalNoMasterFavorites.xml");
            this.AssertNoMasterUpgrade();
        }

        [DeploymentItem(TestDataFiles.TESTDATA_DIRECTORY + NOMASTER_CONFIG_FILE)]
        [DeploymentItem(TestDataFiles.TESTDATA_DIRECTORY + NOMASTER_CREDENTIALS_FILE)]
        [TestMethod]
        public void V2UpgradeNoMasterPasswordCurrentUserTest()
        {
            // Exercise legacy-format migration on every test account without relying on
            // the product's old encryption routine to manufacture its own test vectors.
            string configFile;
            string credentialsFile;
            this.CopyNoMasterFixtures("CurrentUserNoMaster", out configFile, out credentialsFile);
            WriteCurrentUserNoMasterCiphertext(this.CreateFullTestFileName(configFile),
                this.CreateFullTestFileName(credentialsFile));
            this.UpgradePasswordsTestInitialize(configFile, credentialsFile, "CurrentUserNoMasterFavorites.xml");
            this.AssertNoMasterUpgrade();
        }

        private void AssertNoMasterUpgrade()
        {
            IPersistence persistence = this.RunUpgrade();
            Assert.IsFalse(askedForPassword, "Config file shouldn't ask for password");
            bool masterStillValid = PasswordFunctions2.MasterPasswordIsValid(string.Empty, settings.MasterPasswordHash);
            Assert.IsTrue(masterStillValid, "Master password upgrade failed.");
            AssertUserAndCredential(persistence);
        }

        [DeploymentItem(TestDataFiles.TESTDATA_DIRECTORY + SECURED_CONFIG_FILE)]
        [DeploymentItem(TestDataFiles.TESTDATA_DIRECTORY + SECURED_CREDENTIALS_FILE)]
        [TestMethod]
        public void V2UpgradePasswordsTest()
        {
            this.UpgradePasswordsTestInitialize(SECURED_CONFIG_FILE, SECURED_CREDENTIALS_FILE, "favoritesSecured.xml");
            IPersistence persistence = this.RunUpgrade();

            bool masterStillValid = PasswordFunctions2.MasterPasswordIsValid(PasswordTests.MASTERPASSWORD, settings.MasterPasswordHash);
            Assert.IsTrue(masterStillValid, "Master password upgrade failed.");
            AssertUserAndCredential(persistence);
            AssertFavoriteCredentialSet(persistence);
        }

        private static void AssertUserAndCredential(IPersistence persistence)
        {
            // we don't have to authenticate, because it was already done by upgrade
            IFavorite favorite = persistence.Favorites.First();
            var guardedSecurity = new GuardedCredential(favorite.Security, persistence.Security);
            Assert.AreEqual(PasswordTests.USERPASSWORD, guardedSecurity.Password, "Upgrade favorite password failed.");

            ICredentialSet credential = persistence.Credentials.First();
            var guarded = new GuardedCredential(credential, persistence.Security);
            Assert.AreEqual(TEST_PASSWORD, guarded.UserName, "Credential user name upgrade failed.");
            Assert.AreEqual(TEST_PASSWORD, guarded.Password, "Credential password upgrade failed.");
        }

        private static void AssertFavoriteCredentialSet(IPersistence persistence)
        {
            IFavorite favoriteWithCredentials = persistence.Favorites.ToList()[1];
            var credentialsId = favoriteWithCredentials.Security.Credential;
            ICredentialSet credential = persistence.Credentials[credentialsId];
            Assert.IsNotNull(credential, "Favorite referenced credential was lost");
        }

        private IPersistence RunUpgrade()
        {
            var connectionManager = TestConnectionManager.Instance;
            var persistence = FilePersistedTestLab.CreateNotInitializedFilePersistence(new TestFileWatch(), connectionManager);
            var contentUpgrade = new FilesV2ContentUpgrade(persistence, connectionManager, GetMasterPassword);
            contentUpgrade.Run();
            settings.ForceReload(); // because we changed its file, while upgrading
            return persistence;
        }

        private AuthenticationPrompt GetMasterPassword(bool retry)
        {
            Assert.IsFalse(askedForPassword, "Upgrade asks for password second time");
            askedForPassword = true;
            // simulate user prompt for master password
            return new AuthenticationPrompt { Password = PasswordTests.MASTERPASSWORD };
        }

        /// <summary>
        /// because of concurrently running tests, we have to isolate all the files,
        /// otherwise multiple test are writing to the same file and tests fail (specially favorites file).
        /// </summary>
        private void UpgradePasswordsTestInitialize(string configFile, string credentialsFile,
            string favoritesFile = FileLocations.FAVORITES_FILENAME)
        {
            string configFileName = this.CreateFullTestFileName(configFile);
            string favoritesFileName = this.CreateFullTestFileName(favoritesFile);
            string credentialsFileName = this.CreateFullTestFileName(credentialsFile);
            // remove source control read only attribute
            File.SetAttributes(configFileName, FileAttributes.Normal);
            File.SetAttributes(credentialsFileName, FileAttributes.Normal);

            // we have to force all values to test deployment directory,
            // because upgrade works with fully configured files structure
            settings.FileLocations.AssignCustomFileLocations(configFileName, favoritesFileName, credentialsFileName);
            // when running multiple tests, there is may be already old configuration
            settings.ForceReload();
        }

        private void CopyNoMasterFixtures(string prefix, out string configFile, out string credentialsFile)
        {
            configFile = prefix + "Terminals.config";
            credentialsFile = prefix + "Credentials.xml";
            File.Copy(this.CreateFullTestFileName(NOMASTER_CONFIG_FILE),
                this.CreateFullTestFileName(configFile), true);
            File.Copy(this.CreateFullTestFileName(NOMASTER_CREDENTIALS_FILE),
                this.CreateFullTestFileName(credentialsFile), true);
        }

        private static void AssertOriginalNoMasterCiphertextReadable(string configFile, string credentialsFile)
        {
            XDocument configuration = XDocument.Load(configFile);
            XDocument credentials = XDocument.Load(credentialsFile);
            string favoriteCiphertext = configuration.Descendants("favorites").Descendants("add")
                .Select(favorite => favorite.Attribute("encryptedPassword"))
                .Where(password => password != null && !string.IsNullOrEmpty(password.Value))
                .Select(password => password.Value).First();
            string credentialCiphertext = credentials.Descendants("CredentialSet")
                .Select(credential => credential.Element("Password"))
                .Where(password => password != null && !string.IsNullOrEmpty(password.Value))
                .Select(password => password.Value).First();

            // DPAPI CurrentUser ciphertext can only be checked by the original Windows user.
            // Inaccessible historical ciphertext is a fixture limitation, not an upgrade pass.
            try
            {
                Assert.AreEqual(PasswordTests.USERPASSWORD, UnprotectLegacyFixture(favoriteCiphertext),
                    "Committed legacy favorite ciphertext does not match the expected password.");
                Assert.AreEqual(TEST_PASSWORD, UnprotectLegacyFixture(credentialCiphertext),
                    "Committed legacy credential ciphertext does not match the expected password.");
            }
            catch (CryptographicException exception)
            {
                Assert.Inconclusive("Committed CurrentUser DPAPI fixture is inaccessible to this account: "
                    + exception.Message);
            }
        }

        private static string UnprotectLegacyFixture(string ciphertext)
        {
            byte[] protectedBytes = Convert.FromBase64String(ciphertext);
            byte[] plaintext = ProtectedData.Unprotect(protectedBytes, new byte[0],
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }

        private static string ProtectLegacyFixture(string plaintext)
        {
            byte[] protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext),
                new byte[0], DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static void WriteCurrentUserNoMasterCiphertext(string configFile, string credentialsFile)
        {
            // The reference implementation is the framework DPAPI API, not PasswordFunctions.
            string favoriteCiphertext = ProtectLegacyFixture(PasswordTests.USERPASSWORD);
            string credentialCiphertext = ProtectLegacyFixture(TEST_PASSWORD);
            Assert.AreEqual(PasswordTests.USERPASSWORD,
                PasswordFunctions.DecryptPassword(favoriteCiphertext, string.Empty),
                "The legacy decryption routine must accept a framework-produced DPAPI blob.");
            Assert.AreEqual(TEST_PASSWORD,
                PasswordFunctions.DecryptPassword(credentialCiphertext, string.Empty));

            XDocument configuration = XDocument.Load(configFile);
            foreach (XElement favorite in configuration.Descendants("favorites").Descendants("add"))
            {
                XAttribute password = favorite.Attribute("encryptedPassword");
                if (password != null && !string.IsNullOrEmpty(password.Value))
                    password.Value = favoriteCiphertext;
            }
            configuration.Save(configFile);

            XDocument credentials = XDocument.Load(credentialsFile);
            foreach (XElement credential in credentials.Descendants("CredentialSet"))
            {
                XElement password = credential.Element("Password");
                if (password != null && !string.IsNullOrEmpty(password.Value))
                    password.Value = credentialCiphertext;
            }
            credentials.Save(credentialsFile);
        }

        private string CreateFullTestFileName(string fileName)
        {
            string deplymentDir = this.TestContext.DeploymentDirectory;
            return Path.Combine(deplymentDir, fileName);
        }
    }
}
