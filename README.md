install it as a service (admin)
    repmon -install

uninstall it from service (admin)
    repmon -uninstall

run it normallt as an command application
    repmon -normal
    
test email function
    repmon -testmail
    
    
settings template

{
    "smtp": {
        "enabled": true,
        "server":"you email server fqdn or IP",
        "port":587,
        "ssl":true,
        "auth":true,
        "username":"email sender account",
        "password":"email sender password",
        "to":["email recipients"],
        "customize":{
            "subject1":"Notification: MySQL lost sync on replication and it is auto-fixed",
            "subject2":"Alert: MySQL lost sync on replication",        
            "body":"Please check the log at {0} for details"
        }
        
    },
    "period": 300,
    "timeout":10,
    "error_1032": {
        "skips":10,
        "tables":["dualshield.log","dualshield.log_field","dualshield.history"]
        
    } 
}

Note:

subject1 is used for that sync is lost but fixed by monitor, while subject1 is used for worse case which the monitor cannot fix it automatically
skips: it is maximum skips the monitor will try on fixing error 1032.
tables: you can add other tables on skipping the 1032 error.
period: seconds, the monitor will check the replication status per 5 minutes if it is set to 300.
timeout: if MySQL is busy, for instance, when it is running jobs, the status checking will take longer time.

The first version also handle error 1205.


