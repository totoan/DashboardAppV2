import { useState } from "react";
import { setCurrentState } from "../services/signalrService";

function StateSelector() {
    const [selectedState, setSelectedState] = useState("idle");

    async function handleChange(event: React.ChangeEvent<HTMLSelectElement>) {
        const newState = event.target.value;
        setSelectedState(newState);

        try {
            await setCurrentState(newState);
        } catch (error) {
            console.error("Failed to update current state:", error);
        }
    }

    return (
        <div>
            <label>Current State: </label>

            <select value={selectedState} onChange={handleChange}>
                <option value="idle">Idle</option>
                <option value="working">Working</option>
                <option value="gaming">Gaming</option>
                <option value="lunch">Lunch</option>
                <option value="dinner">Dinner</option>
                <option value="sleep">Sleep</option>
            </select>
        </div>
    );
}

export default StateSelector;