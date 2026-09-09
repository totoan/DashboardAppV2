import { useEffect, useRef, useState } from "react";

const BACKEND_URL = "http://localhost:5185";

const SLIDE_INTERVAL = 17000;
const FADE_DURATION = 700;
const HISTORY_SIZE = 5;

function ImageSlideshow() {
  const [images, setImages] = useState<string[]>([]);
  const [currentImage, setCurrentImage] = useState(0);
  const [visible, setVisible] = useState(true);
  const recentImages = useRef<number[]>([]);

  useEffect(() => {
    const loadImages = async () => {
      try {
        const response = await fetch(`${BACKEND_URL}/api/slideshow/images`);

        if (!response.ok) {throw new Error("Failed to load slideshow images.");}

        const imagePaths: string[] = await response.json();
        const fullUrls = imagePaths.map((path) => `${BACKEND_URL}${path}`);

        setImages(fullUrls);

        if (fullUrls.length > 0) {
          const firstIndex = Math.floor(
            Math.random() * fullUrls.length
          );

          setCurrentImage(firstIndex);
          recentImages.current = [firstIndex];
        }
      } catch (error) {
        console.error(
          "[Slideshow] Failed to load images:",
          error
        );
      }
    };

    loadImages();
  }, []);

  const chooseNextImage = () => {
    if (images.length <= 1) {
      return currentImage;
    }

    /* Avoid the last 5 images when possible. */
    const historyLimit = Math.min(HISTORY_SIZE, images.length - 1);
    const blockedImages = recentImages.current.slice(-historyLimit);

    const availableImages = images
      .map((_, index) => index)
      .filter((index) => !blockedImages.includes(index));

    if (availableImages.length === 0) {
      return (currentImage + 1) % images.length;
    }

    const randomPosition = Math.floor(
      Math.random() * availableImages.length
    );

    return availableImages[randomPosition];
  };

  const transitionToNextImage = () => {
    if (images.length <= 1) {
      return;
    }

    // Fade current image out.
    setVisible(false);

    window.setTimeout(() => {
      const nextImage = chooseNextImage();

      setCurrentImage(nextImage);

      recentImages.current = [
        ...recentImages.current,
        nextImage
      ].slice(-HISTORY_SIZE);

      // Fade new image in.
      setVisible(true);
    }, FADE_DURATION);
  };

  useEffect(() => {
    if (images.length <= 1) {
      return;
    }

    const timer = window.setInterval(() => {
      transitionToNextImage();
    }, SLIDE_INTERVAL);

    return () => {
      window.clearInterval(timer);
    };
  }, [images, currentImage]);

  if (images.length === 0) {
    return (
      <div style={{minHeight: "400px", display: "flex", justifyContent: "center", alignItems: "center", color: "white", backgroundColor: "#111", borderRadius: "8px"}}>
        No slideshow images found.
      </div>
    );
  }

  return (
    <div style={{width: "100%", height: "100%", minHeight: "400px", display: "flex", justifyContent: "center", alignItems: "center", overflow: "hidden", backgroundColor: "#111", borderRadius: "8px"}}>
      <img
        src={images[currentImage]}
        alt=""
        style={{width: "100%", height: "100%", maxHeight: "600px", objectFit: "contain",
          opacity: visible ? 1 : 0,
          transition: `opacity ${FADE_DURATION}ms ease-in-out`
        }}
      />
    </div>
  );
}

export default ImageSlideshow;