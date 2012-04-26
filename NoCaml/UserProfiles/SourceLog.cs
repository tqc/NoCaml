using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    public class SourceLog : Dictionary<string, SourceLogEntry>
    {


        public string Serialize()
        {
            var sl = this;

                                        // sourcelog must be less than 2000 chars

                            Func<Dictionary<string, SourceLogEntry>, int, string> slts = (v, mh) => 
                                string.Join(
                                "\n",
                                v.Select(kv => string.Format("{0}|{1}|{2:yyyy-MM-dd HH:mm}|{3}|5|{4}", kv.Value.Field, kv.Value.Source, kv.Value.Updated, kv.Value.User,
                             string.Join("|", kv.Value.SourceLogHistory.Reverse<SourceLogEntry.SourceLogHistoryEntry>().Take(mh).Reverse<SourceLogEntry.SourceLogHistoryEntry>()
                             .Select(he => string.Format("{0}|{1:yyyy-MM-dd HH:mm}|{2}|{3}|{4}", he.Source, he.Updated, he.User, he.Value, he.Hash)).ToArray()
                             )

                             )
                                ).ToArray()
                                );

                            // drop history if the log gets too long
                            var result = slts(sl, 10);
                            if (result.Length >= 2000) result = slts(sl, 9);
                            if (result.Length >= 2000) result = slts(sl, 8);
                            if (result.Length >= 2000) result = slts(sl, 7);
                            if (result.Length >= 2000) result = slts(sl, 6);
                            if (result.Length >= 2000) result = slts(sl, 5);
                            if (result.Length >= 2000) result = slts(sl, 4);
                            if (result.Length >= 2000) result = slts(sl, 3);
                            if (result.Length >= 2000) result = slts(sl, 2);
                            if (result.Length >= 2000) result = slts(sl, 1);
                            if (result.Length >= 2000) result = slts(sl, 0);
                            if (result.Length >= 2000)
                            {
                                // there are other options to reduce the size here, but hopefully they won't be needed
                                result = result.Substring(0, 2000);
                            }
                            return result;
                        }

        

        public void Save () {
        
        
        }

        private void LoadReallyOldFormat(string l)
        {
            //old format - property:source
            if (!l.Contains(":")) return;
            var cl = l.Split(':');

            if (cl.Length < 2) return;
            var entry = new SourceLogEntry()
            {
                Field = cl[0].Trim(),
                Source = cl[1].Trim(),
                Updated = DateTime.MinValue,
                User = "",
                SourceLogHistory = new List<SourceLogEntry.SourceLogHistoryEntry>()
            };
            this[entry.Field] = entry;

        }
        private void LoadOldFormat(string l)
        {
            var cl = l.Split('|');
            if (cl.Length < 4) return;
            var entry = new SourceLogEntry()
            {
                Field = cl[0].Trim(),
                Source = cl[1].Trim(),
                Updated = DateTime.Parse(cl[2].Trim()),
                User = cl[3].Trim(),
                SourceLogHistory = new List<SourceLogEntry.SourceLogHistoryEntry>()
            };

            // after the first 4 columns is audit log
            for (var i = 4; i + 2 < cl.Length; i += 3)
            {
                var he = new SourceLogEntry.SourceLogHistoryEntry()
                {
                    Source = cl[i],
                    Updated = DateTime.Parse(cl[i + 1].Trim()),
                    User = cl[i + 2]
                };
                entry.SourceLogHistory.Add(he);
            }


            this[entry.Field] = entry;


        }
        private void LoadCurrentFormat(string l)
        {
            var cl = l.Split('|');
            if (cl.Length < 5) return;
            var entry = new SourceLogEntry()
            {
                Field = cl[0].Trim(),
                Source = cl[1].Trim(),
                Updated = DateTime.Parse(cl[2].Trim()),
                User = cl[3].Trim(),
                SourceLogHistory = new List<SourceLogEntry.SourceLogHistoryEntry>()
            };
            // 5th column is number of columns in audit log - must be 5


            // after the first 4 columns is audit log
            for (var i = 5; i + 4 < cl.Length; i += 5)
            {
                var he = new SourceLogEntry.SourceLogHistoryEntry()
                {
                    Source = cl[i],
                    Updated = DateTime.Parse(cl[i + 1].Trim()),
                    User = cl[i + 2],
                    Value = cl[i + 3],
                    Hash = cl[i + 4]
                };
                entry.SourceLogHistory.Add(he);
            }


            this[entry.Field] = entry;

        }


        public static SourceLog Load(UserProfileValueCollectionWrapper ppvc)
        {
                                        var sl = (string)ppvc.Value;
                            var dsl = new SourceLog();
                            if (!string.IsNullOrEmpty(sl))
                            {
                                var ll = sl.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var l in ll)
                                {
                                    // editing in the out of box ui can add html tags that should be ignored
                                    if (l.Contains("<")) continue;


                                    // new format - property|source|date|user
                                    if (l.Contains("|"))
                                    {
                                        var cl = l.Split('|');
                                        if (cl.Length > 4 && cl[4] == "5")
                                            dsl.LoadCurrentFormat(l);
                                        else
                                        {
                                            dsl.LoadOldFormat(l);
                                        }
                                    }
                                    else
                                    {
                                        dsl.LoadReallyOldFormat(l);
                                    }
                                }

                            }
                            return dsl;

        }

    }
}
