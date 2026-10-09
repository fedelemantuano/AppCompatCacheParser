# AppCompatCache

AppCompatCache (ShimCache) contains cached file records in a Windows SYSTEM registry hive.

## Language

**Cache entry**:
A cached file record belonging to a ControlSet. Its presence alone does not establish when the file was executed.

**ControlSet**:
A group of Windows system configuration settings in the SYSTEM hive, containing the AppCompatCache records associated with that configuration.

**LastModifiedTimeUTC**:
The file modification timestamp recorded in a cache entry, expressed in UTC when available.
_Avoid_: Execution timestamp, collection timestamp

**SourceFile**:
The SYSTEM hive from which a cache entry was obtained, or the live Registry when no hive file was supplied.
