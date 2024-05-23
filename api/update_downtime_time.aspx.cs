using Aspose.Imaging.Xmp.Schemas.XmpDm;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Toolkit.DataTypes;

public partial class api_update_downtime_time : CPage
{
    string main_url = "https://collectivesolution.atlassian.net/";
    string api_request_ticket_key = "/rest/api/3/issue/"; // GET
    string api_jira_username = "csapi@collectivesolution.net";
    string api_jira_token = "M2NJulCfRoZoNmp8BbLJCFE9";

    //table name for DB
    string table_name = "Portal_Filing_Downtime";


    protected void Page_Load(object sender, EventArgs e)
    {
        int user_id = 0;
        string raw_gettyusername = string.Empty;
        string timezone = string.Empty;
        string employee_name = string.Empty;
        string start_date_local = string.Empty;
        string end_date_local = string.Empty;
        string start_date_utc = string.Empty;
        string end_date_utc = string.Empty;
        string start_date_orig = string.Empty;
        string end_date_orig = string.Empty;
        string raw_campaign = string.Empty;

        string jira_ticket = TypeConversionExtensions.ToString(Request["a"]);

        // Getting the ticket value and remove all spaces
        string ticket = jira_ticket.Replace(" ", "");

        //Instansiate API Method using RestSharp
        RestSharp.Method api_method = Method.GET;
        var client = new RestClient(this.main_url);
        var request = new RestRequest(this.api_request_ticket_key + ticket, api_method);

        // Response.Write(this.api_request_ticket_key + ticket);

        //Encode the credentials to Base64
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(this.api_jira_username + ":" + this.api_jira_token);
        string base64EncodedCredentials = Convert.ToBase64String(plainTextBytes);

        //Adding Headers for Rest API
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", "Basic " + base64EncodedCredentials);

        //Getting the response from the API Get Method and convert it into JObject
        IRestResponse response = client.Execute(request);

        //Validation of response
        if (response.IsSuccessful)
        {

            StringBuilder sb = new StringBuilder();
            var jObject = JObject.Parse(response.Content);
            dynamic x = JsonConvert.DeserializeObject<dynamic>(jObject.ToString(), new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc });

            raw_gettyusername = x.fields.customfield_10062.ToString();
            user_id = this.ReturnEmployeeIDByGettyName(raw_gettyusername);
            //user_id = 2; // FOR DEBUGGING PURPOSES
            employee_name = this.ReturnEmployeeName(user_id);
            var employeeSiteID = this.ReturnEmployeeSiteID(user_id);
            timezone = this.ReturnSiteTimezone(employeeSiteID);
            start_date_utc = x.fields.customfield_10110.ToString(); // It automatically convert to utc from Jira API Data
            end_date_utc = x.fields.customfield_10111.ToString(); // It automatically convert to utc from Jira API Data
            start_date_orig = this.Format_Date_String(this.ReturnUTCToLocal(x.fields.customfield_10110.ToString(), x.fields.assignee.timeZone.ToString()));
            end_date_orig = this.Format_Date_String(this.ReturnUTCToLocal(x.fields.customfield_10111.ToString(), x.fields.assignee.timeZone.ToString()));
            start_date_local = this.Format_Date_String(this.ReturnUTCToLocal(x.fields.customfield_10110.ToString(), timezone));
            end_date_local = this.Format_Date_String(this.ReturnUTCToLocal(x.fields.customfield_10111.ToString(), timezone));
            raw_campaign = x.fields.customfield_10108.value.ToString();

            //Response.Write(start_date_utc + "=======" + start_date_orig + "=======" + start_date_local + "=======" + timezone);
            //Response.End();

            this.Update_Downtime_Record(timezone,ticket,start_date_local,end_date_local,start_date_utc,end_date_utc,start_date_orig,end_date_orig);

            this.ShowFatalSuccess("Jira Update Success");
            return;
        }
    }

    protected void Update_Downtime_Record(string timezone, string jira_ticket, string start_local, string end_local, string start_utc, string end_utc, string start_orig, string end_orig)
    {
        string sql = "UPDATE " + this.table_name + " SET Timezone=@Timezone, StartDate=@StartDate, StartDate_UTC=@StartDate_UTC, EndDate=@EndDate, EndDate_UTC=@EndDate_UTC, Raw_StartDate=@Raw_StartDate, Raw_EndDate=@Raw_EndDate WHERE Jira_Ticket_Number=@Jira_Ticket_Number;";
        this.DB.ClearParameters();
        this.DB.AddParameter(new SqlParameter("@Timezone", timezone));
        this.DB.AddParameter(new SqlParameter("@StartDate", start_local));
        this.DB.AddParameter(new SqlParameter("@StartDate_UTC", start_utc));
        this.DB.AddParameter(new SqlParameter("@EndDate", end_local));
        this.DB.AddParameter(new SqlParameter("@EndDate_UTC", end_utc));
        this.DB.AddParameter(new SqlParameter("@Raw_StartDate", start_orig));
        this.DB.AddParameter(new SqlParameter("@Raw_EndDate", end_orig));
        this.DB.AddParameter(new SqlParameter("@Jira_Ticket_Number", jira_ticket));
        this.DB.ExecuteNonQuery(sql);
    }

    protected string Format_Date_String(string date)
    {
        if (date.Contains("+"))
        {
            return date.Substring(0, date.IndexOf(" +"));
        }

        if (date.Contains("-"))
        {
            return date.Substring(0, date.IndexOf(" -"));
        }

        return date;
    }
}