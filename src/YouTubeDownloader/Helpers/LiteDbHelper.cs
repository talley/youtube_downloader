using DevExpress.XtraEditors.Filtering.Templates;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeDownloader.Helpers
{
    /// <summary>
    ///   LiteDbHelper class
    /// </summary>
    public static class LiteDbHelper
    {

        /// <summary>Inserts the specified data.</summary>
        /// <param name="data">The data.</param>
        /// <returns>
        ///   <br />
        /// </returns>
        public static LiteDbData Insert(LiteDbData data)
        {
            var result=new LiteDbData();
            // Open database (or create if doesn't exist)
            using (var db = new LiteDatabase(@"LiteDbData.db"))
            {
                // Get a collection (or create, if doesn't exist)
                var col = db.GetCollection<LiteDbData>("LiteDbData");

                col.Insert(data);
                result = data;
            }
            return result;
        }


        /// <summary>Gets this instance.</summary>
        /// <returns>
        ///   <br />
        /// </returns>
        public static List<LiteDbData> Get()
        {
            var result = new List<LiteDbData>();
            // Open database (or create if doesn't exist)
            using (var db = new LiteDatabase(@"LiteDbData.db"))
            {
                // Get a collection (or create, if doesn't exist)
                var col = db.GetCollection<LiteDbData>("LiteDbData");


                result = col.FindAll().ToList();
            }
            return result;
        }
    }
}
