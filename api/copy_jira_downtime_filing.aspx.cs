using Aspose.Slides.Export.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Web.UI;
using System.Web.UI.WebControls;
using Telerik.Web.UI;
using Toolkit.DataTypes;

public partial class api_copy_jira_downtime_filing : CPage
{
    string main_url = "https://collectivesolution.atlassian.net/";
    string api_request_ticket_key = "/rest/api/3/issue/"; // GET
    string api_request_thumbnail = "/rest/api/3/attachment/thumbnail/"; // GET
    string api_jira_username = "csapi@collectivesolution.net";
    string api_jira_token = "M2NJulCfRoZoNmp8BbLJCFE9";

    //table name for DB
    string table_name = "Portal_Filing_Downtime";

    string error_log_filename = "jira_cloning_error.txt";

    protected void Page_Load(object sender, EventArgs e)
    {
        //variables for inserting to DB
        //CGlobal global_module = (CGlobal)this.LoadModule("CGlobal");
        int reason_id = 0;
        string reason_text = string.Empty;
        string raw_reason_text = string.Empty;
        int user_id = 0;
        string raw_gettyusername = string.Empty;
        string employee_name = string.Empty;
        string period = string.Empty;
        string control_code = string.Empty;
        string timezone = string.Empty;
        string start_date_local = string.Empty;
        string end_date_local = string.Empty;
        string start_date_utc = string.Empty;
        string end_date_utc = string.Empty;
        string start_date_orig = string.Empty;
        string end_date_orig = string.Empty;
        string raw_campaign = string.Empty;

        int work_location_id = 0;
        string work_location = string.Empty;

        int status_id = 0;
        string status_text = string.Empty;
        string status_label = string.Empty;
        bool is_jira_ticket = false;

        //if (!this.Globals.User.IsAuthenticated) Response.End();

        string jira_ticket = TypeConversionExtensions.ToString(Request["a"]);
        bool is_test_mode = false;
        bool is_validate_mode = false;

        if (TypeConversionExtensions.ToInt32(Request["t"]) >0) is_test_mode = true;
        if (TypeConversionExtensions.ToString(Request["act"]) ==  "Testing") is_validate_mode = true;

        if (jira_ticket.Length == 0)
        {
            this.ShowFatalError("Jira Ticket Number Missing...");
            return;
        }

        // Getting the ticket value and remove all spaces
        string ticket = jira_ticket.Replace(" ", "");

        if (!is_test_mode)
        {
            // Check if Jira exists in the DB or it is test mode.
            bool is_jira_ticket_number_exist = this.IsTicketExist(ticket);
            if (is_jira_ticket_number_exist)
            {
                this.ShowFatalError("Jira Ticket Number already exists...");
                return;
            }
        }

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

            string fieldcheck_error = string.Empty;
            var isValid = this.IsValidateFields(x, ref fieldcheck_error);

            if (!isValid)
            {
                string error = "Jira Ticket: ["+ticket+"] FieldCheck: ["+ fieldcheck_error + "]";
                this.Log(error, error_log_filename);
                this.ShowFatalError("Jira Cloning FieldCheck Error: ["+ fieldcheck_error + "]");
                return;
            }

            if (is_validate_mode) 
            {
                if(fieldcheck_error.Length > 0)
                {
                    Response.Write(fieldcheck_error);
                }
                
                this.ShowFatalSuccess("Validate Done...");
                
                return; 
            }

            if (x.fields.customfield_10066 == null)
            {
                work_location_id = 0;
                work_location = "None";
            }
            else
            {
                string locationValue = x.fields.customfield_10066.value.ToString();

                if (locationValue.ToLower() == "on-site")
                {
                    work_location_id = 1;
                    work_location = locationValue;
                }

                if (locationValue.ToLower() == "wfh")
                {
                    work_location_id = 2;
                    work_location = locationValue;
                }

            }

            //Deducing the missing fields for Data table
            is_jira_ticket = true;
            reason_id = this.ReturnReasonIDBYJiraReasonText(x.fields.customfield_10109.value.ToString());
            reason_text = this.ReturnReasonTextByID(reason_id);
            raw_reason_text = x.fields.customfield_10109.value.ToString();
            raw_gettyusername = x.fields.customfield_10062.ToString();
            user_id = this.ReturnEmployeeIDByGettyName(raw_gettyusername);
            //user_id = 2; // FOR DEBUGGING PURPOSES
            employee_name = this.ReturnEmployeeName(user_id);
            var employeeSiteID = this.ReturnEmployeeSiteID(user_id);
            period = this.ReturnSiteLocalYear(this.ReturnSiteTimezone(employeeSiteID)) + "-" + this.ReturnSiteLocalMonth(this.ReturnSiteTimezone(employeeSiteID));
            control_code = this.ReturnUniqueCode(period, this.table_name);
            timezone = this.ReturnSiteTimezone(employeeSiteID);
            start_date_utc = x.fields.customfield_10110.ToString(); // It automatically convert to utc from Jira API Data
            end_date_utc = x.fields.customfield_10111.ToString(); // It automatically convert to utc from Jira API Data
            start_date_orig = this.Format_Date_String(this.ReturnUTCToLocal(x.fields.customfield_10110.ToString(), x.fields.assignee.timeZone.ToString()));
            end_date_orig = this.Format_Date_String(this.ReturnUTCToLocal(x.fields.customfield_10111.ToString(), x.fields.assignee.timeZone.ToString()));
            start_date_local = this.Format_Date_String(this.ReturnUTCToLocal(x.fields.customfield_10110.ToString(), timezone));
            end_date_local = this.Format_Date_String(this.ReturnUTCToLocal(x.fields.customfield_10111.ToString(), timezone));
            raw_campaign = x.fields.customfield_10108.value.ToString();

            //Response.Write(start_date_utc + "=======" + start_date_orig +"======="+start_date_local + "=======" + user_id);
            //Response.End();

            if (x.fields.customfield_10107 == null)
            {
                status_id = 0;
                status_text = "Pending";
                status_label = "None";
            }
            else
            {
                string statusValue = x.fields.customfield_10107.value.ToString();

                if (statusValue.ToLower() == "yes")
                {
                    status_id = 1;
                    status_text = "Valid";
                    status_label = statusValue;
                }

                if (statusValue.ToLower() == "no")
                {
                    status_id = 2;
                    status_text = "Invalid";
                    status_label = statusValue;
                }

            }

            //sb.AppendLine("Reason ID: " + reason_id);
            //sb.AppendLine("Reason Text: " + reason_text);
            //sb.AppendLine("Raw Reason Text: " + raw_reason_text);
            //sb.AppendLine("User ID: " + user_id);
            //sb.AppendLine("Employee Name: " + employee_name);
            //sb.AppendLine("Period: " + period);
            //sb.AppendLine("Control Code: " + control_code);
            //sb.AppendLine("Timezone: " + timezone);
            //sb.AppendLine("Start Time Local: " + start_date_local);
            //sb.AppendLine("End Time Local: " + end_date_local);
            //sb.AppendLine("Start Time UTC: " + start_date_utc);
            //sb.AppendLine("End Time UTC: " + end_date_utc);
            //sb.AppendLine("Start Time ORIG: " + start_date_orig);
            //sb.AppendLine("End Time ORIG: " + end_date_orig);
            //sb.AppendLine("Campaign: " + raw_campaign);
            //sb.AppendLine("Work Location ID: " + work_location_id);
            //sb.AppendLine("Work Location: " + work_location);
            //sb.AppendLine("Jira Ticket: " + ticket.ToString());
            //sb.AppendLine("Status ID: " + status_id.ToString());
            //sb.AppendLine("Status Text: " + status_text);
            //sb.AppendLine("Status Label: " + status_label);

            this.Add_Downtime_Record(x, period, control_code, user_id, work_location_id, reason_id, timezone, start_date_local, end_date_local, ticket, status_id, status_text, raw_gettyusername, raw_campaign, start_date_orig, end_date_orig, is_jira_ticket);

            HashList info_list = new HashList();
            info_list.Add("Name", employee_name);
            info_list.Add("GettyUserName", raw_gettyusername);
            info_list.Add("Jira Ticket Number", jira_ticket);
            info_list.Add("Jira Valid Downtime Ticket Status", status_label);
            info_list.Add("Status", status_text);
            info_list.Add("Location", work_location);
            info_list.Add("Start Date/Time", start_date_local);
            info_list.Add("End Date/Time", end_date_local);
            info_list.Add("Reason", reason_text);

            //bool email_sent = this.SendEmail(user_id, info_list);

            //if (email_sent)
            //{
            //    sb.AppendLine("Email Sent!");
            //}
            //else
            //{
            //    sb.AppendLine("Email Not Sent...");
            //}

            //string parsed_data = sb.ToString();
            //Response.Write(parsed_data);
            //Response.End();
            this.Cache.DeleteCache("approvals_*");
            this.ShowFatalSuccess("Jira Cloning Success");
            return;
        }
        else
        {
            string error = response.StatusDescription;
            this.Log(error, error_log_filename);
            this.ShowFatalError(error);
            this.ShowFatalError("Jira Cloning Error: [" + error + "]");
            return;
        }
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

    protected void Add_Downtime_Record(dynamic x, string period, string control_code, int emp_id, int work_location_id, int reason_id, string timezone, string start_local, string end_local, string jira_ticket, int status_id, string status_label, string raw_gettyusername, string raw_campaign, string raw_start_date, string raw_end_date, bool is_jira_ticket)
    {
        string sql = @"INSERT INTO " + table_name + @"(Period, Control_Code, EmployeeID, WorkLocationID, ReasonID, Timezone, StartDate, StartDate_UTC, EndDate, EndDate_UTC, Jira_Ticket_Number, Jira_Ticket_StatusID, Jira_Ticket_Status, FiledBy, Date_Created, Raw_GettyUserName, Raw_Campaign, Raw_StartDate, Raw_EndDate, IsJira) VALUES (@Period, @Control_Code, @EmployeeID, @WorkLocationID, @ReasonID, @Timezone, @StartDate, Tzdb.LocalToUtc(@StartDate,@TimeZone,DEFAULT,DEFAULT), @EndDate, Tzdb.LocalToUtc(@EndDate,@TimeZone,DEFAULT,DEFAULT), @Jira_Ticket_Number, @Jira_Ticket_StatusID, @Jira_Ticket_Status, @EmployeeID, GETUTCDATE(), @Raw_GettyUserName, @Raw_Campaign, @Raw_StartDate, @Raw_EndDate, @IsJira);
	 DECLARE @ID INT;SELECT @ID=SCOPE_IDENTITY();SELECT @ID;";
        this.DB.ClearParameters();
        this.DB.AddParameter(new SqlParameter("@Period", period));
        this.DB.AddParameter(new SqlParameter("@Control_Code", control_code));
        this.DB.AddParameter(new SqlParameter("@EmployeeID", emp_id));
        this.DB.AddParameter(new SqlParameter("@WorkLocationID", work_location_id));
        this.DB.AddParameter(new SqlParameter("@ReasonID", reason_id));
        this.DB.AddParameter(new SqlParameter("@Timezone", timezone));
        this.DB.AddParameter(new SqlParameter("@StartDate", start_local));
        this.DB.AddParameter(new SqlParameter("@EndDate", end_local));
        this.DB.AddParameter(new SqlParameter("@Jira_Ticket_Number", jira_ticket));
        this.DB.AddParameter(new SqlParameter("@Jira_Ticket_StatusID", status_id));
        this.DB.AddParameter(new SqlParameter("@Jira_Ticket_Status", status_label));
        this.DB.AddParameter(new SqlParameter("@Raw_GettyUserName", raw_gettyusername));
        this.DB.AddParameter(new SqlParameter("@Raw_Campaign", raw_campaign));
        this.DB.AddParameter(new SqlParameter("@Raw_StartDate", raw_start_date));
        this.DB.AddParameter(new SqlParameter("@Raw_EndDate", raw_end_date));
        this.DB.AddParameter(new SqlParameter("@IsJira", is_jira_ticket));

        int new_id = TypeConversionExtensions.ToInt32(this.DB.ExecuteScalar(sql));

        this.Add_JSON_Event(table_name, new_id, "EmployeeID", "Added");

        this.StartUploadProcessing(new_id, x);

        this.LogEvent(User_Module.Downtime, User_Event.Downtime_Approval, 0, "transferred jira ticket ["+ jira_ticket + "] of username:[" + raw_gettyusername + "]", new_id);

    }

    #region File Uploading
    Hashtable ProcessedFiles = new Hashtable();
    protected void StartUploadProcessing(int id, dynamic x)
    {
        int file_count = 0;
        file_count = x.fields.attachment.Count;
        if (file_count == 0) return;

        string config_target_folder = WebConfigurationManager.AppSettings["Folder_Downtime"];
        string final_folder = this.ReturnFolder(config_target_folder);

        string extension = string.Empty;
        string filename = string.Empty;
        string orig_filename = string.Empty;
        int file_id = 0;

        foreach (var item in x.fields.attachment)
        {
            var client = new RestClient(this.main_url);
            var requestThumbnail = new RestRequest(this.api_request_thumbnail + item.id.ToString(), Method.GET);

            byte[] plainTextBytes = Encoding.UTF8.GetBytes(this.api_jira_username + ":" + this.api_jira_token);
            string base64EncodedCredentials = Convert.ToBase64String(plainTextBytes);

            requestThumbnail.AddHeader("Content-Type", "application/json");
            requestThumbnail.AddHeader("Authorization", "Basic " + base64EncodedCredentials);

            IRestResponse responseThumbnail = client.Execute(requestThumbnail);

            if (responseThumbnail.IsSuccessful)
            {

                orig_filename = item.filename.ToString();
                var orig_filename_lower = orig_filename.ToLower();

                if (!this.ProcessedFiles.ContainsKey(orig_filename_lower))
                {
                    byte[] fileData = responseThumbnail.RawBytes;
                    string temp_folder = Server.MapPath("../temp/downtime/");

                    this.ProcessedFiles.Add(orig_filename_lower, orig_filename_lower);

                    extension = Path.GetExtension(orig_filename_lower);
                    file_id = this.AddFileToTable("Portal_Filing_Downtime_Files", "DowntimeID", id, extension.ToLower(), orig_filename_lower, ref filename);

                    File.WriteAllBytes(temp_folder + filename, fileData);

                    string[] final_files = Directory.GetFiles(temp_folder);

                    foreach (string file in final_files)
                    {
                        string file_name = Path.GetFileName(file);

                        string dest_file = Path.Combine(final_folder, file_name);
                        if (File.Exists(dest_file)) { File.Delete(dest_file); }
                        File.Copy(file, dest_file, true);
                    }
                }
            }
        }
    }
    #endregion

    #region Send Email Functions
    protected bool SendEmail(int user_id, HashList info_list)
    {

        bool email_sent = false;
        string info_table = this.ReturnInfoTable(info_list, "Downtime Application Details");

        CGlobal module = (CGlobal)this.LoadModule("CGlobal");
        string name = module.GetEmployeeName(user_id);
        string email = string.Empty;
        if (module.GetIsEmailNotification(user_id)) email = module.GetEmailAddress(user_id);

        int department_id = module.GetDepartmentID(user_id);
        string department_notify_list = module.ReturnDepartmentNotifyList(department_id, user_id);

        string hr_list = string.Empty;

        Hashtable ht = new Hashtable();
        this.Populate_Global_EmailTemplate_Data(ref ht, user_id);
        ht["INFO_TABLE"] = info_table;
        ht["EMPLOYEE_NAME"] = name;

        string filepath = Server.MapPath("templates/st_master.htm");
        string template = File.ReadAllText(filepath);
        string content1 = this.ReturnEmailMessage(ref ht, 1, template);
        string content2 = this.ReturnEmailMessage(ref ht, 2, template);

        string subject = "Email Alert: Downtime Application - " + name;
        try
        {
            if (email.Length > 0) this.SendEmail(email, "", "", subject, content1);
            if (department_notify_list.Length > 0) this.SendEmail(department_notify_list, "", "", subject, content2);
            email_sent = true;
        }
        catch (Exception ex)
        {
            string error = ex.ToString();
            email_sent = false;
        }
        return email_sent;
    }

    protected string ReturnEmailMessage(ref Hashtable ht, int level_id, string template)
    {
        StringBuilder sb = new StringBuilder(template);
        switch (level_id)
        {
            case 1: ht["MESSAGE"] = "[STANDARD_GREETING]<p>This is to confirm your application with the following details:</p>"; break;
            case 2: ht["MESSAGE"] = "<p>You have another application to approve with the following details:</p>"; break;
        }

        string body = CTools.FillUpEmailTemplate(sb.ToString(), ht);
        body = CTools.FillUpEmailTemplate(body, ht);
        string final_body = CTools.FillUpEmailTemplate(body, ht);
        var result = PreMailer.Net.PreMailer.MoveCssInline(final_body);
        return result.Html;
    }
    #endregion

    protected bool IsValidateFields(dynamic x, ref string error_message)
    {
        error_message = string.Empty;
        if (x.fields.customfield_10066 == null) { error_message = "Missing work_location Field"; return false; } // variable work_location
        if (x.fields.customfield_10107 == null) { error_message = "Missing is_valid_downtime field"; return false; } // variable is_valid_downtime
        if (x.fields.customfield_10108 == null) { error_message = "Missing campaign field"; return false; } // variable campaign
        if (x.fields.customfield_10109 == null) { error_message = "Missing reason field"; return false; } // variable reason
        if (x.fields.customfield_10062 == null) { error_message = "Missing getty_username field"; return false; } // variable getty_username
        if (x.fields.customfield_10110 == null) { error_message = "Missing start_time_downtime_issue field"; return false; } // variable start_time_downtime_issue
        if (x.fields.customfield_10111 == null) { error_message = "Missing end_time_downtime_issue field"; return false; } // variable end_time_downtime_issue
        return true;
    }

    protected bool IsTicketExist(string jira_ticket)
    {
        string sql = @"SELECT COUNT(*) FROM Portal_Filing_Downtime (NOLOCK) WHERE Jira_Ticket_Number=@Jira_Ticket_Number;";

        this.DB.ClearParameters();
        this.DB.AddParameter(new SqlParameter("@Jira_Ticket_Number", jira_ticket));

        int existing_ticket = TypeConversionExtensions.ToInt32(this.DB.ExecuteScalar(sql));

        if (existing_ticket > 0) { return true; }

        return false;
    }

    protected void AppendAllBytes(string path, byte[] bytes)
    {
        //argument-checking here.

        using (var stream = new FileStream(path, FileMode.Append))
        {
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}