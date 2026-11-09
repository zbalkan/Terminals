# How to store data in SQL database

Note: This feature is under active development and isn't present in any of published versions.

Terminals supports two possibilities how to store user data. If you don't change any application options the data are saved into local xml based files (for more details see [Backup and restore settings files](Backup-and-restore-settings-files.md)). Because the  possibilities of sharing files are limited, we implemented the same functionality using SQL database. Currently **only Microsoft SQL server** is supported, tested on version 2008 Express.

## How to create database
Create an empty SQL Server database using the version-controlled script `Resources/Database/Terminals_CreateDatabase_v3.0.sql`. Run it on the target SQL Server instance using a SQL administration tool (for example, `sqlcmd -S .\SQLEXPRESS -E -b -i Resources\Database\Terminals_CreateDatabase_v3.0.sql`). The script creates the `TERMINALS` database and its schema. There are **no prebuilt MDF/LDF files** in the project: SQL Server creates and manages those physical files. Existing installations must retain and back up their actual database files; removing sample binaries from source control does not migrate or delete deployed databases. The database is used only as a simple store. Currently (version 3.0) it contains only a few tables, stored procedures and relations. There is no extra logic like triggers, indexes or in database stored CLR code etc.
Because the usage of data from performance point of view is not well known, there are no special indexes or other optimization. Any performance optimization proposals from database administrators are welcome.
The empty database in this case doesn't mean empty database, it means database filled with required model structure (tables, relations and stored procedures), but without any value in all tables.

The default SQL connection now targets `.\SQLEXPRESS`, database `TERMINALS`, using integrated security; it does not attach a database file. For remote SQL Server installations, configure the server and database in the connection string as usual.

### Existing installations that used MDF auto-attachment

This changes only the suggested **default** connection string. Existing saved connection strings are not automatically rewritten. An installation already configured to connect to a named SQL Server database can continue using that database without a schema migration.

If an existing installation uses `AttachDbFilename=...` (and possibly `User Instance=...`), **do not replace the connection string blindly** or run the database-creation script against its existing data. First back up the database and verify where the MDF/LDF files are attached. If necessary, have a SQL Server administrator attach the existing database or restore its backup on a supported SQL Server instance. Then update Terminals to use that instance and database name, for example `Data Source=.\SQLEXPRESS;Initial Catalog=TERMINALS;Integrated Security=True`, and verify favorites, groups, credentials, and permissions before retiring the previous configuration.

The SQL scripts shipped with this repository create a fresh database; they do **not** upgrade an existing one. This change does not introduce an automatic schema migration or convert an existing database to a newer version.

The historical scripts in `Resources/Database/Migrations/1.0.0` are **SQL scripts, not EF6 Code First migrations**. They remain as source material; they are not automatically executed by the application or test runner. Do not assume they implement a supported database upgrade pipeline. A future migration system must support upgrades of existing databases, stored procedures, version tracking, and rollback/recovery.

## How to configure the connection
To distribute the database connection in your environment, start Terminals on one of your machines, go to application options and under "**Data Store**" you will find relevant options. To successfully connect to the database, configure:

* **Connection string**: Usual connection string used to connect to the MS SQL database. When the SQL storage is enabled, default connection string is offered as an example. For more details consult the MS SQL documentation.

* **Database password**: Because we have to protect the shared content stored in the database, SQL database is simply protected by this password. The password is checked when Terminals starts. Note, that this is not the application master password, nor the connection string password. Default database password is empty.

Both values are stored as a part of application configuration and is stored like other parts in encrypted form. The "**Test connection**" button allows you check, if application is able to connect to the database. It doesn't check the database structure. Anyway, if the database isn't available during application startup, the data store will be switched back to file persisted.

Following image describes the configuration in more detail:

![Sql Persistence](Store-data-in-SQL-database_Terminals_SqlPersistence.PNG)
* **Server Name** "..." button: Use Sql browser service to locate SQL instances in your network
* **Database** "..." button: Locate all available databases attached to the selected database server instance
* **Database password**: Provide current Terminals database master password set up by your application administrator. This is used to validate, when connecting to the database.
* **Set password**: Configure the database with new database password. You will be prompted to provide old and new password like in other password resets. If old password doesn't match, you will be not able to apply new password. Depending on content of your database this procedure can take a while, because it requires to update all already stored passwords.
* **Test connection**: Creates new connection string from currently configured values and tries to connect to terminals database. Result is shown, when procedure finishes. Not only connection string is checked, but also provided database master password is checked. Using this button you are able to check complete connection setup.
* **Advanced**: If you need to provide more detail about your connection, like timeouts etc. this button shows detail properties dialog, where you are able to configure complete MS SQL connection string.

## SQL store behavior
* Only simple concurrent updates are implemented. E.g. In some cases the database request may fail. In this case restart the application
* Simple refresh is done each 30 seconds. So wait few seconds after your colleague saves favorite changes
* There are no private favorites or credentials. All team members share complete database content
* The database is shared for all users, there is no possibility to isolate favorites for each user. There is no user or groups management.
* Database Master password updates are done in transaction. It should be secure to update all your stored passwords, if you already have them stored by clients.
* If you forget database master password, go to the options table and clear "MasterPasswordHash" row value. In this case, there is no restore. All your passwords protected by this database master password are lost.

## How to migrate the data from files to the database
* To switch between data store types you have to restart the application
* Changing the data store doesn't copy, change or remove any data
* If you use both data stored, their data are isolated, but they share application configuration
* There is no possibility to combine favorites between file store and database store
* To migrate the content you have to export favorites, restart the application and import the exported content
