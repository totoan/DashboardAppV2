import { useEffect, useState } from "react";

const BACKEND_URL = "http://localhost:5185";

type NovaState = "offline" | "starting up" | "ready";

interface NovaStatusResponse {
  status: NovaState;
}

function NovaStatus() {
  const [status, setStatus] = useState<NovaState>("offline");

  useEffect(() => {
    const checkNova = async () => {
      try {
        const response = await fetch(
          `${BACKEND_URL}/api/nova/status`
        );

        if (!response.ok) {
          throw new Error("Failed to check Nova status.");
        }

        const data: NovaStatusResponse = await response.json();

        setStatus(data.status);
      } catch {
        setStatus("offline");
      }
    };

    checkNova();

    const timer = window.setInterval(checkNova, 3000);

    return () => {
      window.clearInterval(timer);
    };
  }, []);

  const getColor = () => {
    switch (status) {
      case "ready":
        return "#66ff99";

      case "starting up":
        return "#ffd166";

      default:
        return "#ff6666";
    }
  };

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: "8px",
        color: "white"
      }}
    >
      <div
        style={{
          width: "10px",
          height: "10px",
          borderRadius: "50%",
          backgroundColor: getColor(),
          boxShadow:
            status === "ready"
              ? `0 0 8px ${getColor()}`
              : "none"
        }}
      />

      <span>
        Nova: {status}
      </span>
    </div>
  );
}

export default NovaStatus;