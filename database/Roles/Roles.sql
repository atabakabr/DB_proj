CREATE ROLE ManagerRole;
CREATE ROLE SupervisorRole;
CREATE ROLE GuardRole;
CREATE ROLE StudentRole;

DENY EXECUTE TO PUBLIC;

GRANT EXECUTE ON AddStudentToRoomGroup TO ManagerRole, SupervisorRole;
GRANT EXECUTE ON RemoveStudentFromRoomGroup TO ManagerRole, SupervisorRole;
GRANT EXECUTE ON ReserveRoomForGroup TO StudentRole;
GRANT EXECUTE ON usp_GetGuardTrafficReport TO GuardRole;
