import numpy as np
import cv2
import glob
import EasyPySpin

def undistort(image, calibration_data):
    ret, mtx, dist, rvecs, tvecs = calibration_data

    # Refine the camera matrix
    h, w = image.shape[:2]
    newcameramtx, roi = cv2.getOptimalNewCameraMatrix(mtx, dist, (w,h), 1, (w,h))

    # Undistort the frame
    image = cv2.undistort(image, mtx, dist, None, newcameramtx)

    return image

def order_points(pts):
    # Initialize a list of coordinates that will be ordered
    # such that the first entry in the list is the top-left,
    # the second entry is the top-right, the third is the bottom-right,
    # and the fourth is the bottom-left
    rect = np.zeros((4, 2), dtype="float32")

    # The top-left point will have the smallest sum,
    # whereas the bottom-right point will have the largest sum
    s = pts.sum(axis=1)
    rect[0] = pts[np.argmin(s)]
    rect[2] = pts[np.argmax(s)]

    # The top-right point will have the smallest difference,
    # whereas the bottom-left will have the largest difference
    diff = np.diff(pts, axis=1)
    rect[1] = pts[np.argmin(diff)]
    rect[3] = pts[np.argmax(diff)]

    return rect

def get_corners(image, grid_n, grid_m, criteria):
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    ret, corners = cv2.findChessboardCorners(gray, (grid_n, grid_m), None)
    if ret == True:
        outer_corners_indices = [0, grid_n-1, (grid_m-1)*grid_n, grid_m*grid_n-1]
        outer_corners = np.array([corners[i] for i in outer_corners_indices])
        outer_corners = cv2.cornerSubPix(gray, outer_corners, (11, 11), (-1, -1), criteria)
        return outer_corners.reshape(-1, 2)

    else:
        print("Could not find the outer corners")
        return []

# Function to calculate unit vector
def unit_vector(vector):
    return vector / np.linalg.norm(vector)

# Function to calculate the distance between two points
def distance(p1, p2):
    return np.linalg.norm(p1 - p2)

# This function will perform a perspective transform based
# on an extrapolation of the outer 4 corners of a chessboard.
def four_point_transform_and_expand(image, pts, grid_n, grid_m):
    # Obtain a consistent order of the points and unpack them
    rect = order_points(pts)

    # Calculate dimensions of the rectangle
    width = distance(rect[0], rect[1])  # Distance between tl and tr
    height = distance(rect[1], rect[2])  # Distance between tr and br

    # Calculate the side length of one square
    square_side_x = width / grid_m
    square_side_y = height / grid_n

    # Assuming squares are perfect squares, use the average side length
    d = (square_side_x + square_side_y) / 2

    # Calculate vectors along the edges
    vec_top = rect[1] - rect[0]  # tr - tl
    vec_right = rect[2] - rect[1]  # br - tr
    vec_bottom = rect[3] - rect[2]  # bl - br
    vec_left = rect[0] - rect[3]  # tl - bl

    # Normalize the edge vectors
    unit_top = unit_vector(vec_top)
    unit_right = unit_vector(vec_right)
    unit_bottom = unit_vector(vec_bottom)
    unit_left = unit_vector(vec_left)

    # We expand to the left and right by two squares and the height will
    # keep an aspect ratio of 16x9
    tl = rect[0] + (1 + 9.0 / 16.0) * unit_left * d - 2 * unit_top * d
    tr = rect[1] + 2 * unit_top * d - (1 + 9.0 / 16.0) * unit_right * d
    br = rect[2] + (1 + 9.0 / 16.0) * unit_right * d - 2 * unit_bottom * d
    bl = rect[3] + 2 * unit_bottom * d - (1 + 9.0 / 16.0) * unit_left * d

    rect = np.array([tl, tr, br, bl])

    # Compute the width of the new image, which will be the
    # maximum distance between bottom-right and bottom-left
    # x-coordinates or the top-right and top-left x-coordinates
    widthA = np.linalg.norm(br - bl)
    widthB = np.linalg.norm(tr - tl)
    maxWidth = max(int(widthA), int(widthB))

    # Compute the height of the new image, which will be the
    # maximum distance between the top-right and bottom-right
    # y-coordinates or the top-left and bottom-left y-coordinates
    heightA = np.linalg.norm(tr - br)
    heightB = np.linalg.norm(tl - bl)
    maxHeight = max(int(heightA), int(heightB))

    # Now that we have the dimensions of the new image, construct
    # the set of destination points to obtain a "birds eye view",
    # (i.e. top-down view) of the image, again specifying points
    # in the top-left, top-right, bottom-right, and bottom-left order
    dst = np.array([
        [0, 0],
        [maxWidth - 1, 0],
        [maxWidth - 1, maxHeight - 1],
        [0, maxHeight - 1]], dtype="float32")

    # Compute the perspective transform matrix and then apply it
    M = cv2.getPerspectiveTransform(rect, dst)
    warped = cv2.warpPerspective(image, M, (maxWidth, maxHeight))

    # Return the warped image
    return warped

# Cap is a cv2 capture object.
# Grid_n is the width, grid_m the height.
# Calibrate uses glob.glob to obtain png samples.
# Criteria is the termination criteria
def calibrate(image, grid_n, grid_m, glob_path, criteria):
    """
    Callibrate the camera
    """
 
    # prepare object points, like (0,0,0), (1,0,0), (2,0,0) ....,(6,5,0)
    objp = np.zeros((grid_m*grid_n,3), np.float32)
    objp[:,:2] = np.mgrid[0:grid_n,0:grid_m].T.reshape(-1,2)
 
    # Arrays to store object points and image points from all the images.
    objpoints = [] # 3d point in real world space
    imgpoints = [] # 2d points in image plane.

    # Read from calibration sample images
    images = glob.glob(glob_path)

    for fname in images:
        img = cv2.imread(fname)
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

        # Find the chess board corners
        ret, corners = cv2.findChessboardCorners(gray, (grid_n, grid_m), None)

        # If found, add object points, image points (after refining them)
        if ret == True:
            objpoints.append(objp)

            corners2 = cv2.cornerSubPix(gray, corners, (11, 11), (-1, -1), criteria)
            imgpoints.append(corners2)

            # Draw the corners
            cv2.drawChessboardCorners(img, (grid_n, grid_m), corners2, ret)
        else:
            print(str(fname) + ": No chessboard found while calibrating. Can the camera view the entire board?")
            return None

        # Calibrate the camera
        ret, mtx, dist, rvecs, tvecs = cv2.calibrateCamera(objpoints, imgpoints, gray.shape[::-1], None, None)

    return (ret, mtx, dist, rvecs, tvecs)