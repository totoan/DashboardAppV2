function Calendar (){

    const today = new Date();

    return(
        <div className="calendar-container" style={{ margin: "20px" }}>
            <h2>{today.toLocaleDateString()}</h2>
            <div className="calendar">
            </div>
        </div>
    );
}

export default Calendar;