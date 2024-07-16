import cv2
import json
import numpy as np
import requests
import warnings
from datetime import datetime
from ultralytics import YOLO
from sklearn.decomposition import PCA

import EasyPySpin
import calibrate as cb

# Load the YOLOv8 model
model = YOLO("weights/best.pt")

# Open the camera
# cap = cv2.VideoCapture(0)
cap = EasyPySpin.VideoCapture(0)

# Set the checkerboard grid size, where 
# grid_n is the height and grid_m is the width.
# These widths represent the inner vertices, not the squares.
grid_n, grid_m = 8, 15

# Set to true to not run the model and just create training data.
# When set, calibration is still done and you can press 's' to save an image.
create_training_data = False
disable_calibration = False
disable_networking = False

# Set the server address
server_address = "http://192.168.0.179:8080"
#server_address = "http://localhost:8080"

# Set the minimum confidence value to detect an object
confidence = 0.6


# Start a session
session = None
if not disable_networking:
    print("Starting a session with the Unity server...")
    session = requests.Session()

# Calibration data
calibration_data = None
corners = None
# termination criteria
criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 30, 0.001)
recalculate_corners = False

# Loop through the video frames
while cap.isOpened():
    # Read a frame from the video
    success, raw_frame = cap.read()

    # Make sure we are using color (spinnaker)
    if (len(raw_frame.shape) != 3):
        raw_frame = cv2.cvtColor(raw_frame, cv2.COLOR_BayerBG2BGR)

    if success:
        # Calibrate camera using checkerboard pattern.
        if calibration_data is None and not disable_calibration:
            print("Starting calibration.")
            calibration_data = cb.calibrate(raw_frame, grid_n, grid_m, 'calibration_samples/*.png', criteria)
            undistorted_frame = cb.undistort(raw_frame, calibration_data)
            corners = cb.get_corners(undistorted_frame, grid_n, grid_m, criteria)
            print("Calibration finished!")
            continue

        if recalculate_corners is True:
            corners = cb.get_corners(undistorted_frame, grid_n, grid_m, criteria)
            recalculate_corners = False

        undistorted_frame = raw_frame
        if not disable_calibration:
            undistorted_frame = cb.undistort(raw_frame, calibration_data)

        if (corners is not None and len(corners) == 4):
            frame = cb.four_point_transform_and_expand(undistorted_frame, corners, grid_n, grid_m)
        else:
            frame = undistorted_frame

        if create_training_data is True:
            cv2.imshow("frame", frame)
            key = cv2.waitKey(30)
            if key == ord("q"):
                break
            elif key == ord("c"):
                recalculate_corners = True
                print("Recalculating corners")
            elif key == ord("s"):
                print("saving")
                cv2.imwrite(datetime.now().strftime("%H%M%S") + '.png', frame)


            continue

        height, width, _ = frame.shape

        # Run YOLOv8 inference on the frame
        results = model.predict(frame, conf=confidence, device=0, imgsz=[640, 640], verbose=False)

        # Obtain the masks, bounding boxes and names of the results
        results_json = json.loads(results[0].tojson())
        boxes = [obj["box"] for obj in results_json]
        masks = [[obj["segments"]["x"], obj["segments"]["y"]] for obj in results_json]
        names = [obj["name"] for obj in results_json]

        # DEBUGGING
        print(frame.shape)
        print(names)
        print(boxes)
        print('\n')

        # Copy the frame
        annotated_frame = frame.copy()

        # Dictionary used to send information to the Unity server, following the format.
        # We will use the default Unity json tools, so we can only use one dimensional arrays,
        # because of this, we need to keep track of the amount of points each mask contains.
        detection_info = {
            'max_width': int(width),
            'max_height': int(height),
            'bboxes': [],
            'masks': [],
            'mask_lengths': [],
            'angles': [],
            'names': []
        }

        # We want to send the bboxes to Unity
        for box in boxes:
            x1 = int(box["x1"])
            y1 = int(box["y1"])
            x2 = int(box["x2"])
            y2 = int(box["y2"])

            detection_info["bboxes"].extend([x1, y1, x2, y2])

        # We want to send the mask coordinates and the amount of coordinates to Unity,
        # as well as the angle, which will be determined by PCA on the mask.
        for mask in masks:
            x, y = mask
            coordinates = list(zip(x, y))

            detection_info["masks"].extend([int(coord) for pair in coordinates for coord in pair])
            detection_info["mask_lengths"].append(len(coordinates) // 2)

            if len(coordinates) == 0:
                warnings.warn("The input array 'coordinates' is empty.")
                continue

            # Convert coordinates to a NumPy array
            coordinates_np = np.array(coordinates)

            # Check the shape of the array
            if coordinates_np.ndim == 1:
                # Reshape the array if it's 1D
                coordinates_np = coordinates_np.reshape(-1, 1)

            # Perform PCA
            pca = PCA(n_components=2)
            pca.fit(coordinates_np)
            principal_components = pca.components_

            # Calculate the angle of the first principal component
            angle = np.arctan2(principal_components[0, 1], principal_components[0, 0]) * 180 / np.pi
            detection_info["angles"].append(int(angle))

        # We want to send the names to Unity
        for name in names:
            detection_info["names"].append(name)

        # Send the information to Unity
        if not disable_networking:
            session.post(server_address, json=detection_info)

        # Standard YOLO plot
        annotated_frame = results[0].plot()

        # Display the annotated frame
        cv2.imshow("'YOLOv8' Inference", annotated_frame)

        # Break the loop if 'q' is pressed, and calibrate when 'c' is pressed
        key = cv2.waitKey(30)
        if key == ord("q"):
            break
        elif key == ord("c"):
            recalculate_corners = True
            print("Recalculating corners")
        elif key == ord("s"):
            print("saving")
            cv2.imwrite(datetime.now().strftime("%H%M%S") + '.png', annotated_frame)
        elif key == ord("p"):
            while True:
                cv2.imshow("'YOLOv8' Inference", annotated_frame)
                key = cv2.waitKey(30)
                if key == ord("p"):
                    break
    else:
        # Break the loop if the end of the video is reached
        break

# End the session
if not disable_networking:
    session.close()

# Release the video capture object and close the display window
cap.release()
cv2.destroyAllWindows()