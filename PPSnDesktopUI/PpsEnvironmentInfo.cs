#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace TecWare.PPSn
{
   #region -- class PpsEnvironmentInfo -------------------------------------------------

   ///////////////////////////////////////////////////////////////////////////////
   /// <summary></summary>
   public sealed class PpsEnvironmentInfo : IEquatable<PpsEnvironmentInfo>
   {
      private readonly string name;
      private XDocument content;

      private readonly DirectoryInfo localPath;
      private readonly FileInfo infoFile;

      List<RecentLogin> recentUsers = new List<RecentLogin>();

      public PpsEnvironmentInfo(string name)
      {
         this.name = name;

         this.localPath = new DirectoryInfo(Path.GetFullPath(Path.Combine(localEnvironmentsPath, name)));
         if (!localPath.Exists)
            localPath.Create();

         this.infoFile = new FileInfo(Path.Combine(localPath.FullName, "info.xml"));

         ReadInfoFile();
      } // ctor

      public override bool Equals(object obj)
         => Equals(obj as PpsEnvironmentInfo);

      public bool Equals(PpsEnvironmentInfo other)
      {
         if (Object.ReferenceEquals(this, other))
            return true;
         else if (Object.ReferenceEquals(other, null))
            return false;
         else
            return localPath.FullName.Equals(other.LocalPath.FullName);
      } // func Equals

      public override int GetHashCode()
         => localPath.FullName.GetHashCode();

      private void ReadInfoFile()
      {
         if (infoFile.Exists)
         {
            content = XDocument.Load(infoFile.FullName);
            LoadRecentUsers(content, ref recentUsers);
         }
         else
            content = new XDocument(new XElement("ppsn"));
      } // proc

      private static void LoadRecentUsers(XDocument content, ref List<RecentLogin> recentUsers)
      {
         if (content == null)
            return;
         if (content.Descendants("ppsn").First() == null)
            return;
         if (!content.Descendants("ppsn").First().HasElements)
            return;
         if (!content.Descendants("ppsn").First().Descendants("login").First().HasElements)
            return;
         foreach (var ru in content.Descendants("ppsn").First().Descendants("login").First().Descendants())
         {
            if (ru.Name == "recentuser")
               recentUsers.Add(new RecentLogin(ru.GetAttribute("username", String.Empty), ru.GetAttribute<DateTime>("timestamp", DateTime.MinValue)));
         }
         recentUsers.Sort((a, b) => (b.Timestamp.CompareTo(a.Timestamp)));
      }

      public string LastUser
      {
         set
         {
            // get the persitent settings for login
            var ppsn = content.Descendants("ppsn")?.First();
            var login = ppsn.HasElements ? ppsn.Descendants("login")?.First() : null;
            if (login == null)
            {
               login = new XElement("login");
               ppsn.Add(login);
            }

            // find the actual user in memory
            var reoccuringUser = recentUsers.Find(t => t.UserName == value);

            XElement newLogin = null;
            
            if (reoccuringUser == null)
            {
               // new user - create it
               reoccuringUser = (new RecentLogin(value));
               recentUsers.Add(reoccuringUser);
               newLogin = new XElement("recentuser");
               newLogin.SetAttributeValue("username", value);
               login.Add(newLogin);
            }
            else
            {
               // existing user - select in persistent settings
               var logins = login.Descendants().GetEnumerator();
               while (logins.MoveNext())
               {
                  if (logins.Current.Name == "recentuser")
                     if (logins.Current.GetAttribute("username", String.Empty) == value)
                        newLogin = logins.Current;
               }
            }

            // set the time
            reoccuringUser.Timestamp = DateTime.Now;
            newLogin.SetAttributeValue("timestamp", reoccuringUser.Timestamp);
            // save to persistent setting
            content.Save(infoFile.FullName);
         }
         get
         {
            if (recentUsers.Any())
               return (from user in recentUsers select recentUsers.OrderByDescending(t => t.Timestamp).FirstOrDefault()).First().UserName;
            else
               return String.Empty;
         }
      }

      public void Update(XElement xNewInfo)
      {
         // copy uri
         xNewInfo.SetAttributeValue("uri", Uri);

         if (!Procs.CompareNode(content.Root, xNewInfo))
         {
            content = new XDocument(xNewInfo);
            content.Save(infoFile.FullName);
         }
      } // proc UpdateInfoFile
            
      public class RecentLogin
      {
         public string UserName;
         public DateTime Timestamp;

         public RecentLogin(string userName, DateTime timestamp)
         {
            this.UserName = userName;
            this.Timestamp = timestamp;
         }

         public RecentLogin(string userName)
         {
            this.UserName = userName;
         }
      }
      
      public IEnumerable<string> RecentUsers
      {
         get
         {
            var users = new List<string>();
            foreach (var user in recentUsers)
               if (users.IndexOf(user.UserName) < 0)
                  users.Add(user.UserName);
            return users;
         }
      }

      public string Name => name;

      public string DisplayName { get { return content.Root.GetAttribute("displayName", name); } set { content.Root.SetAttributeValue("displayName", value); } }
      public Uri Uri
      {
         get
         {
            var uri = content.Root.GetAttribute("uri", null);
            return uri == null ? null : new Uri(uri);
         }
         set { content.Root.SetAttributeValue("uri", value.ToString()); }
      } // prop Uri

      public Version Version { get { return new Version(content.Root.GetAttribute("version", "0.0.0.0")); } set { content.Root.SetAttributeValue("version", value.ToString()); } }

      public DirectoryInfo LocalPath => localPath;

      // -- static --------------------------------------------------------------

      private static string localEnvironmentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ppsn", "env");

      public static bool operator ==(PpsEnvironmentInfo a, PpsEnvironmentInfo b)
         => !Object.ReferenceEquals(a, null) && a.Equals(b);

      public static bool operator !=(PpsEnvironmentInfo a, PpsEnvironmentInfo b)
         => Object.ReferenceEquals(a, null) || !a.Equals(b);

      public static PpsEnvironmentInfo CreateEnvironment(string serverName, Uri serverUri)
      {
         var info = new PpsEnvironmentInfo(serverName);
         if (info.Uri == null) // update server uri
            info.Uri = serverUri;
         return info;
      } // func CreateEnvironment

      public static IEnumerable<PpsEnvironmentInfo> GetLocalEnvironments()
      {
         var localEnvironmentsDirectory = new DirectoryInfo(localEnvironmentsPath);
         if (localEnvironmentsDirectory.Exists)
         {
            foreach (var cur in localEnvironmentsDirectory.EnumerateDirectories())
            {
               PpsEnvironmentInfo localEnvironment = null;
               try
               {
                  localEnvironment = new PpsEnvironmentInfo(cur.Name);
               }
               catch (Exception e)
               {
                  Debug.Print(e.ToString());
               }
               if (localEnvironment != null)
                  yield return localEnvironment;
            }
         }
      } // func GetLocalEnvironments
   } // class PpsEnvironmentInfo

   #endregion
}
